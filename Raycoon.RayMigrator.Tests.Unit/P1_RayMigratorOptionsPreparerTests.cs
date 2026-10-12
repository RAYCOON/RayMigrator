using System.Text.Json;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;
using Raycoon.RayMigrator.Validation;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="RayMigratorOptionsPreparer"/> gives options a host built in memory the same treatment the CLI
/// gives options bound from files: placeholders, defaults, enum probe, data annotations and the rule catalog.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class RayMigratorOptionsPreparerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "raymigrator-prep-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _variables = [];

    public RayMigratorOptionsPreparerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var variable in _variables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private string SetVariable(string value)
    {
        string name = "RM_PREP_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Environment.SetEnvironmentVariable(name, value);
        _variables.Add(name);
        return name;
    }

    private RayMigratorOptions CreateOptions(string targetConnectionString)
    {
        var options = ProfileTestContext.CreateOptions(targetConnectionString);
        options.Products![0].MigrationFilesRootDirectory = _dir;
        return options;
    }

    [Fact]
    public void ReplaceWithEnvironmentVariables_WalksStringsArraysDictionariesAndNestedLists()
    {
        string dirVar = SetVariable(_dir);
        string toolVar = SetVariable("sqlite3");
        string hostVar = SetVariable("db.example.test");
        string codeVar = SetVariable("0");

        var options = CreateOptions($"Data Source={{ENV:{dirVar}}}/target.db");
        options.Products![0].MigrationFilesRootDirectory = $"{{ENV:{dirVar}}}";
        options.Products[0].TargetGroups![0].Targets![0].CliToolParameters = new Dictionary<string, string> { ["Host"] = $"{{ENV:{hostVar}}}" };
        options.CliTools =
        [
            new CliToolOptions { Alias = "sqlite", ExecutablePath = $"{{ENV:{toolVar}}}", ArgumentTemplate = "{FilePath}", InputMode = "File", SuccessExitCodes = [$"{{ENV:{codeVar}}}"] }
        ];

        var replaced = EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(options);

        options.Products[0].MigrationFilesRootDirectory.Should().Be(_dir);
        options.Products[0].TargetGroups![0].Targets![0].ConnectionString.Should().Be($"Data Source={_dir}/target.db");
        options.Products[0].TargetGroups![0].Targets![0].CliToolParameters!["Host"].Should().Be("db.example.test");
        options.CliTools[0].ExecutablePath.Should().Be("sqlite3");
        options.CliTools[0].SuccessExitCodes![0].Should().Be("0");

        replaced.Select(r => r.Path).Should().BeEquivalentTo(
        [
            "RayMigrator:Products:0:MigrationFilesRootDirectory",
            "RayMigrator:Products:0:TargetGroups:0:Targets:0:ConnectionString",
            "RayMigrator:Products:0:TargetGroups:0:Targets:0:CliToolParameters:Host",
            "RayMigrator:CliTools:0:ExecutablePath",
            "RayMigrator:CliTools:0:SuccessExitCodes:0",
        ], "every replaced value is reported with the configuration path it was found at");
        replaced.Should().OnlyContain(r => r.EnvironmentVariableValue != null);
        replaced.Single(r => r.Path.EndsWith("ExecutablePath")).EnvironmentVariableName.Should().Be(toolVar);
    }

    [Fact]
    public void ReplaceWithEnvironmentVariables_MissingVariable_ReportsNullValue()
    {
        var options = CreateOptions("Data Source={ENV:RM_PREP_DOES_NOT_EXIST}/target.db");

        var replaced = EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(options);

        replaced.Should().ContainSingle().Which.EnvironmentVariableValue.Should().BeNull();
        options.Products![0].TargetGroups![0].Targets![0].ConnectionString.Should().Be("Data Source=/target.db",
            "the placeholder is removed and the caller decides whether a missing variable is an error");
    }

    [Fact]
    public void Prepare_ResolvesPlaceholdersAndRegistersTheValuesWithTheMasker()
    {
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        string dirVar = SetVariable(_dir);
        var options = CreateOptions($"Data Source={{ENV:{dirVar}}}/target.db");

        var result = RayMigratorOptionsPreparer.Prepare(options);

        options.Products![0].TargetGroups![0].Targets![0].ConnectionString.Should().Be($"Data Source={_dir}/target.db");
        result.ReplacedEnvironmentVariables.Should().ContainSingle().Which.EnvironmentVariableName.Should().Be(dirVar);
        SensitiveDataMasker.Mask($"path [{_dir}] and [{options.Repository!.ConnectionString}]")
            .Should().NotContain(_dir, "a resolved environment variable value is a secret to the engine")
            .And.NotContain("Data Source=:memory:", "the connection strings of the options are registered as well");
    }

    [Fact]
    public void Prepare_CredentialFromPlaceholder_IsNotReportedAsHardcoded()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        string pwdVar = SetVariable("s3cr3t");
        var options = CreateOptions($"Data Source={_dir}/target.db;Password={{ENV:{pwdVar}}}");
        string asWritten = options.Products![0].TargetGroups![0].Targets![0].ConnectionString!;

        var replaced = EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(options);
        var section = RayMigratorOptionsPreparer.AsWrittenSection(replaced);

        section.Should().NotBeNull();
        section!["Products:0:TargetGroups:0:Targets:0:ConnectionString"].Should().Be(asWritten,
            "the rule catalog sees the value as written, with the placeholder");
        RayMigratorOptionsValidator.GetReport(options, section).Warnings.Should().NotContain(w => w.Code == RuleIds.RULE_7_3,
            "the credential enters through a placeholder (#30)");
        RayMigratorOptionsValidator.GetReport(options).Warnings.Should().Contain(w => w.Code == RuleIds.RULE_7_3,
            "the resolved value alone looks hardcoded, which is what a host without an as-written section got before");

        var act = () => RayMigratorOptionsPreparer.Prepare(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void Prepare_MissingVariable_ThrowsNamingVariableAndPath()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = CreateOptions("Data Source={ENV:RM_PREP_DOES_NOT_EXIST}/target.db");

        var act = () => RayMigratorOptionsPreparer.Prepare(options);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*RM_PREP_DOES_NOT_EXIST*")
            .WithMessage("*RayMigrator:Products:0:TargetGroups:0:Targets:0:ConnectionString*");
    }

    [Fact]
    public void Prepare_WritesTheDocumentedDefaultsIntoNullProperties()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = CreateOptions("Data Source=:memory:");
        options.Repository!.DbCommandTimeoutInSeconds = null;
        options.Repository.DbCommandMaxRetries = null;
        options.Repository.DbCommandWaitTimeInMsBeforeRetry = null;
        var target = options.Products![0].TargetGroups![0].Targets![0];
        target.DbCommandTimeoutInSeconds = null;
        target.DbCommandMaxRetries = null;
        target.DbCommandWaitTimeInMsBeforeRetry = null;

        RayMigratorOptionsPreparer.Prepare(options);

        options.Repository.DbCommandTimeoutInSeconds.Should().Be(60, "the data annotation writes its default back, as it does for options bound from files");
        options.Repository.DbCommandMaxRetries.Should().Be(100);
        options.Repository.DbCommandWaitTimeInMsBeforeRetry.Should().Be(250);
        target.DbCommandTimeoutInSeconds.Should().Be(20, "the product defaults are merged down before the annotations run");
        target.DbCommandMaxRetries.Should().Be(0);
        target.DbCommandWaitTimeInMsBeforeRetry.Should().Be(250);
    }

    [Fact]
    public void Prepare_InvalidEnumValue_Throws()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = CreateOptions("Data Source=:memory:");
        options.Products![0].MigrationErrorAction = "Explode";

        var act = () => RayMigratorOptionsPreparer.Prepare(options);

        act.Should().Throw<ConfigurationValidationException>().WithMessage("*Explode*");
    }

    [Fact]
    public void Prepare_RuleCatalogError_Throws()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = CreateOptions("Data Source=:memory:");
        var targets = options.Products![0].TargetGroups![0].Targets!;
        targets.Add(new TargetOptions { Alias = targets[0].Alias, ConnectionString = "Data Source=:memory:" });

        var act = () => RayMigratorOptionsPreparer.Prepare(options);

        act.Should().Throw<ConfigurationValidationException>().WithMessage($"*{ProfileTestContext.TargetAlias}*");
    }

    [Fact]
    public void Prepare_Twice_LeavesTheOptionsUnchanged()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        string dirVar = SetVariable(_dir);
        var options = CreateOptions($"Data Source={{ENV:{dirVar}}}/target.db");
        options.Repository!.DbCommandTimeoutInSeconds = null;

        RayMigratorOptionsPreparer.Prepare(options);
        string first = JsonSerializer.Serialize(options);
        var second = RayMigratorOptionsPreparer.Prepare(options);

        JsonSerializer.Serialize(options).Should().Be(first);
        second.ReplacedEnvironmentVariables.Should().BeEmpty("the placeholders were already resolved");
    }
}
