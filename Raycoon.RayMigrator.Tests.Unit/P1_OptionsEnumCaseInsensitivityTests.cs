using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1 (#3): The nine *Enum getters on the options classes must parse configuration values with the same
/// case-insensitive contract that RayEnumAttribute validates with. Before this was fixed, a case variant
/// such as "rollback" passed startup validation and then silently became Undefined at runtime — which
/// downstream code treats like Terminate. An unparseable non-null value now fails fast instead.
/// </summary>
public class OptionsEnumCaseInsensitivityTests
{
    #region Case variants — one non-canonical spelling per getter (and more)

    [Theory]
    [InlineData("rollback", MigrationErrorAction.Rollback)]
    [InlineData("ROLLBACK", MigrationErrorAction.Rollback)]
    [InlineData("rollbackerroronly", MigrationErrorAction.RollbackErrorOnly)]
    [InlineData("rollbackRelease", MigrationErrorAction.RollbackRelease)]
    [InlineData("terminate", MigrationErrorAction.Terminate)]
    [InlineData("ignore", MigrationErrorAction.Ignore)]
    [InlineData("Rollback", MigrationErrorAction.Rollback)]
    public void ProductDefaults_MigrationErrorActionEnum_IsCaseInsensitive(string value, MigrationErrorAction expected)
    {
        new ProductDefaultOptions { MigrationErrorAction = value }.MigrationErrorActionEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("rollback", MigrationErrorAction.Rollback)]
    [InlineData("ROLLBACK", MigrationErrorAction.Rollback)]
    [InlineData("rollbackerroronly", MigrationErrorAction.RollbackErrorOnly)]
    [InlineData("Rollback", MigrationErrorAction.Rollback)]
    public void Product_MigrationErrorActionEnum_IsCaseInsensitive(string value, MigrationErrorAction expected)
    {
        new ProductOptions { MigrationErrorAction = value }.MigrationErrorActionEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("ignore", RollbackErrorAction.Ignore)]
    [InlineData("TERMINATE", RollbackErrorAction.Terminate)]
    [InlineData("Terminate", RollbackErrorAction.Terminate)]
    public void ProductDefaults_RollbackErrorActionEnum_IsCaseInsensitive(string value, RollbackErrorAction expected)
    {
        new ProductDefaultOptions { RollbackErrorAction = value }.RollbackErrorActionEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("ignore", RollbackErrorAction.Ignore)]
    [InlineData("TERMINATE", RollbackErrorAction.Terminate)]
    [InlineData("Ignore", RollbackErrorAction.Ignore)]
    public void Product_RollbackErrorActionEnum_IsCaseInsensitive(string value, RollbackErrorAction expected)
    {
        new ProductOptions { RollbackErrorAction = value }.RollbackErrorActionEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("simultaneously", TargetMigrationOrder.Simultaneously)]
    [InlineData("SIMULTANEOUSLY", TargetMigrationOrder.Simultaneously)]
    [InlineData("successively", TargetMigrationOrder.Successively)]
    [InlineData("Successively", TargetMigrationOrder.Successively)]
    public void TargetGroupDefaults_TargetMigrationOrderEnum_IsCaseInsensitive(string value, TargetMigrationOrder expected)
    {
        new TargetGroupDefaultOptions { TargetMigrationOrder = value }.TargetMigrationOrderEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("simultaneously", TargetMigrationOrder.Simultaneously)]
    [InlineData("SIMULTANEOUSLY", TargetMigrationOrder.Simultaneously)]
    [InlineData("successively", TargetMigrationOrder.Successively)]
    public void TargetGroup_TargetMigrationOrderEnum_IsCaseInsensitive(string value, TargetMigrationOrder expected)
    {
        new TargetGroupOptions { TargetMigrationOrder = value }.TargetMigrationOrderEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("disabled", HashValidationScope.Disabled)]
    [InlineData("sqlblocks", HashValidationScope.SqlBlocks)]
    [InlineData("SqlBlocks", HashValidationScope.SqlBlocks)]
    [InlineData("SQLBLOCKS", HashValidationScope.SqlBlocks)]
    [InlineData("file", HashValidationScope.File)]
    public void TargetGroupDefaults_HashValidationScopeEnum_IsCaseInsensitive(string value, HashValidationScope expected)
    {
        new TargetGroupDefaultOptions { HashValidationScope = value }.HashValidationScopeEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("disabled", HashValidationScope.Disabled)]
    [InlineData("sqlblocks", HashValidationScope.SqlBlocks)]
    [InlineData("FILE", HashValidationScope.File)]
    public void TargetGroup_HashValidationScopeEnum_IsCaseInsensitive(string value, HashValidationScope expected)
    {
        new TargetGroupOptions { HashValidationScope = value }.HashValidationScopeEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("stdin", CliToolInputMode.Stdin)]
    [InlineData("STDIN", CliToolInputMode.Stdin)]
    [InlineData("file", CliToolInputMode.File)]
    public void CliTool_InputModeEnum_IsCaseInsensitive(string value, CliToolInputMode expected)
    {
        new CliToolOptions { InputMode = value }.InputModeEnum.Should().Be(expected);
    }

    [Fact]
    public void SurroundingWhitespace_IsNotTrimmed_LikeRayEnumAttribute()
    {
        // ConfigurationBinder does not trim and RayEnumAttribute compares the raw string; the getter must not be more lenient.
        var act = () => new ProductOptions { MigrationErrorAction = " Rollback " }.MigrationErrorActionEnum;

        act.Should().Throw<ConfigurationValidationException>();
    }

    #endregion

    #region Null / whitespace keeps the "inherit from defaults" sentinel

    [Fact]
    public void NullOrWhitespace_ReturnsUndefined_OrFileForInputMode()
    {
        new ProductDefaultOptions().MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Undefined);
        new ProductDefaultOptions().RollbackErrorActionEnum.Should().Be(RollbackErrorAction.Undefined);
        new TargetGroupDefaultOptions().TargetMigrationOrderEnum.Should().Be(TargetMigrationOrder.Undefined);
        new TargetGroupDefaultOptions().HashValidationScopeEnum.Should().Be(HashValidationScope.Undefined);
        new ProductOptions().MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Undefined);
        new ProductOptions().RollbackErrorActionEnum.Should().Be(RollbackErrorAction.Undefined);
        new TargetGroupOptions().TargetMigrationOrderEnum.Should().Be(TargetMigrationOrder.Undefined);
        new TargetGroupOptions().HashValidationScopeEnum.Should().Be(HashValidationScope.Undefined);
        new CliToolOptions().InputModeEnum.Should().Be(CliToolInputMode.File);

        new ProductOptions { MigrationErrorAction = "   " }.MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Undefined);
        new CliToolOptions { InputMode = "" }.InputModeEnum.Should().Be(CliToolInputMode.File);
    }

    [Fact]
    public void NullValue_IsNotCached_SoLaterDefaultsMergeIsPickedUp()
    {
        // ProductDefaultsPostConfigureOptions copies defaults into the product after binding.
        // A getter that was read before the merge must see the merged value afterwards.
        var product = new ProductOptions();
        product.MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Undefined);

        product.MigrationErrorAction = "rollback";

        product.MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Rollback);
    }

    #endregion

    #region Fail fast on unparseable non-null values (Admin-DB / pre-built options bypass validation)

    [Fact]
    public void Product_MigrationErrorAction_Typo_Throws_InsteadOfReturningUndefined()
    {
        var product = new ProductOptions { MigrationErrorAction = "Rollbak" };

        var act = () => product.MigrationErrorActionEnum;

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Invalid value [Rollbak] for property [MigrationErrorAction]. " +
                         "Allowed values: [Terminate, Rollback, RollbackErrorOnly, RollbackRelease, Ignore].*");
    }

    [Fact]
    public void ErrorMessage_MatchesRayEnumAttributeWording()
    {
        // Validator and parser must speak with one voice: same "Invalid value [..] for property [..]. Allowed values: [..]." shape.
        var attribute = new Core.Configuration.Validation.RayAttributes.RayEnumAttribute(typeof(MigrationErrorAction), isRequired: true);
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(new ProductOptions()) { MemberName = "MigrationErrorAction" };
        var validatorMessage = attribute.GetValidationResult("Rollbak", context)!.ErrorMessage!;

        var act = () => new ProductOptions { MigrationErrorAction = "Rollbak" }.MigrationErrorActionEnum;

        act.Should().Throw<ConfigurationValidationException>().Which.Message.Should().EndWith(validatorMessage);
    }

    [Theory]
    [InlineData("Undefined")]
    [InlineData("undefined")]
    [InlineData("1")]
    [InlineData("Rollback,Ignore")]
    public void SentinelNumericAndFlagsSpellings_AreRejected(string value)
    {
        // RayEnumAttribute only accepts the member names after Undefined; a raw Enum.TryParse would
        // have accepted all of these. The getter must not be more lenient than the validator.
        var act = () => new ProductOptions { MigrationErrorAction = value }.MigrationErrorActionEnum;

        act.Should().Throw<ConfigurationValidationException>();
    }

    [Fact]
    public void AllNineGetters_ThrowOnInvalidValue()
    {
        var invalid = "NotAMember";

        ((Func<object>)(() => new ProductDefaultOptions { MigrationErrorAction = invalid }.MigrationErrorActionEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new ProductDefaultOptions { RollbackErrorAction = invalid }.RollbackErrorActionEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new TargetGroupDefaultOptions { TargetMigrationOrder = invalid }.TargetMigrationOrderEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new TargetGroupDefaultOptions { HashValidationScope = invalid }.HashValidationScopeEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new ProductOptions { MigrationErrorAction = invalid }.MigrationErrorActionEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new ProductOptions { RollbackErrorAction = invalid }.RollbackErrorActionEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new TargetGroupOptions { TargetMigrationOrder = invalid }.TargetMigrationOrderEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new TargetGroupOptions { HashValidationScope = invalid }.HashValidationScopeEnum)).Should().Throw<ConfigurationValidationException>();
        ((Func<object>)(() => new CliToolOptions { InputMode = invalid }.InputModeEnum)).Should().Throw<ConfigurationValidationException>();
    }

    #endregion

    #region End to end — JSON bind + data annotations + defaults merge (the path that hid the bug)

    private static RayMigratorOptions BindAndValidate(string json)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<RayMigratorOptions>()
            .Configure(options => configuration.GetSection("RayMigrator").Bind(options))
            .ValidateDataAnnotations();
        services.AddTransient<IPostConfigureOptions<RayMigratorOptions>, ProductDefaultsPostConfigureOptions>();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
    }

    private static string MinimalJson(string productMigrationErrorAction, string targetGroupHashValidationScope, string cliToolInputMode)
    {
        var root = System.Text.Json.JsonSerializer.Serialize(Path.GetTempPath());
        return $$"""
        {
          "RayMigrator": {
            "ProductDefaults": {
              "MigrationErrorAction": "Terminate",
              "RollbackErrorAction": "terminate",
              "MigrationFilesExtension": "sql",
              "MigrationRollbackFilesPreExtension": "rollback",
              "MigrationFilesEncoding": "UTF-8",
              "TargetGroupDefaults": {
                "TargetMigrationOrder": "successively",
                "HashValidationScope": "File",
                "TargetDefaults": { "DbCommandTimeoutInSeconds": 20 }
              }
            },
            "Products": [
              {
                "Alias": "P",
                "MigrationFilesRootDirectory": {{root}},
                "MigrationErrorAction": "{{productMigrationErrorAction}}",
                "TargetGroups": [
                  {
                    "Alias": "Backend",
                    "DatabaseType": "Sqlite",
                    "HashValidationScope": "{{targetGroupHashValidationScope}}",
                    "Targets": [
                      { "Alias": "Main", "ConnectionString": "Data Source=:memory:" }
                    ]
                  }
                ]
              }
            ],
            "CliTools": [
              {
                "Alias": "sqlite3",
                "ExecutablePath": "sqlite3",
                "ArgumentTemplate": "{FilePath}",
                "InputMode": "{{cliToolInputMode}}"
              }
            ]
          }
        }
        """;
    }

    [Fact]
    public void JsonCaseVariants_PassValidation_AndParseToTheIntendedMembers()
    {
        // This is the scenario from the issue: "rollback" on a product validates OK (RayEnumAttribute is
        // case-insensitive) and previously reached the runtime as Undefined == "behave like Terminate".
        var options = BindAndValidate(MinimalJson("rollback", "disabled", "stdin"));

        var product = options.Products!.Single();
        product.MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Rollback);
        product.RollbackErrorActionEnum.Should().Be(RollbackErrorAction.Terminate, "merged from ProductDefaults (\"terminate\")");

        var targetGroup = product.TargetGroups!.Single();
        targetGroup.HashValidationScopeEnum.Should().Be(HashValidationScope.Disabled);
        targetGroup.TargetMigrationOrderEnum.Should().Be(TargetMigrationOrder.Successively, "merged from TargetGroupDefaults (\"successively\")");

        options.CliTools!.Single().InputModeEnum.Should().Be(CliToolInputMode.Stdin);
    }

    [Fact]
    public void JsonCanonicalValues_StillParse()
    {
        var options = BindAndValidate(MinimalJson("Rollback", "Disabled", "Stdin"));

        options.Products!.Single().MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Rollback);
        options.Products!.Single().TargetGroups!.Single().HashValidationScopeEnum.Should().Be(HashValidationScope.Disabled);
        options.CliTools!.Single().InputModeEnum.Should().Be(CliToolInputMode.Stdin);
    }

    [Fact]
    public void JsonTypo_IsStillRejectedAtStartup_WithRayEnumWordingAndLocation()
    {
        // Typos never reach the runtime. They are reported by the PostConfigure probe (which runs before
        // DataAnnotation validation reflects over the getters) with the same wording RayEnumAttribute uses.
        var act = () => BindAndValidate(MinimalJson("Rollbak", "Disabled", "Stdin"));

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Products[0] (Alias 'P'): Invalid value [Rollbak] for property [MigrationErrorAction]. " +
                         "Allowed values: [Terminate, Rollback, RollbackErrorOnly, RollbackRelease, Ignore].*");
    }

    [Fact]
    public void JsonMultipleTypos_AreAllReportedInOneException()
    {
        var act = () => BindAndValidate(MinimalJson("Rollbak", "Disable", "Pipe"));

        act.Should().Throw<ConfigurationValidationException>()
            .Which.Message.Should()
                .Contain("3 enum-typed configuration value(s) could not be parsed")
                .And.Contain("Products[0] (Alias 'P'): Invalid value [Rollbak] for property [MigrationErrorAction]")
                .And.Contain("Products[0].TargetGroups[0] (Alias 'Backend'): Invalid value [Disable] for property [HashValidationScope]")
                .And.Contain("CliTools[0] (Alias 'sqlite3'): Invalid value [Pipe] for property [InputMode]");
    }

    #endregion
}
