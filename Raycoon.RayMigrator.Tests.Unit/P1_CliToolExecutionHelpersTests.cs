
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: MigrationService CLI tool execution helper tests.
/// ResolveUseCliToolAlias and ResolveCliToolArguments are critical for routing
/// migration files to the correct execution path (CLI vs DAL).
/// </summary>
public class ResolveUseCliToolAliasTests
{
    [Fact]
    public void ResolveUseCliToolAlias_FileAliasSet_ReturnsFileAlias()
    {
        var file = new MigrationFileInfo { UseCliToolAlias = "sqlcmd" };
        var target = new TargetOptions { UseCliToolAlias = "psql" };

        var result = MigrationService.ResolveUseCliToolAlias(file, target);

        result.Should().Be("sqlcmd");
    }

    [Fact]
    public void ResolveUseCliToolAlias_FileAliasNull_ReturnsTargetAlias()
    {
        var file = new MigrationFileInfo { UseCliToolAlias = null };
        var target = new TargetOptions { UseCliToolAlias = "psql" };

        var result = MigrationService.ResolveUseCliToolAlias(file, target);

        result.Should().Be("psql");
    }

    [Fact]
    public void ResolveUseCliToolAlias_BothNull_ReturnsNull()
    {
        var file = new MigrationFileInfo { UseCliToolAlias = null };
        var target = new TargetOptions { UseCliToolAlias = null };

        var result = MigrationService.ResolveUseCliToolAlias(file, target);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveUseCliToolAlias_FileAliasTakesPrecedenceOverTargetAlias()
    {
        var file = new MigrationFileInfo { UseCliToolAlias = "sqlite3" };
        var target = new TargetOptions { UseCliToolAlias = "sqlcmd" };

        var result = MigrationService.ResolveUseCliToolAlias(file, target);

        result.Should().Be("sqlite3");
    }

    [Fact]
    public void ResolveUseCliToolAlias_FileAliasEmpty_ReturnsEmptyString_NotTargetAlias()
    {
        // Empty string is technically non-null; ?? only triggers on null
        var file = new MigrationFileInfo { UseCliToolAlias = string.Empty };
        var target = new TargetOptions { UseCliToolAlias = "sqlcmd" };

        var result = MigrationService.ResolveUseCliToolAlias(file, target);

        // file.UseCliToolAlias is "" (not null), so ?? does not fall through to target
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void ResolveUseCliToolAlias_TargetAliasSet_FileAliasNull_ReturnsTargetAlias()
    {
        var file = new MigrationFileInfo { UseCliToolAlias = null };
        var target = new TargetOptions { UseCliToolAlias = "mariadb" };

        var result = MigrationService.ResolveUseCliToolAlias(file, target);

        result.Should().Be("mariadb");
    }
}

/// <summary>
/// P1: ResolveCliToolArguments placeholder substitution tests.
/// Incorrect substitution leads to wrong CLI invocations at runtime.
/// </summary>
public class ResolveCliToolArgumentsTests
{
    private static CliToolOptions BuildTool(string template) => new CliToolOptions
    {
        Alias = "test-tool",
        ExecutablePath = "sqlcmd",
        ArgumentTemplate = template
    };

    private static TargetOptions BuildTarget(Dictionary<string, string>? parameters = null) =>
        new TargetOptions
        {
            Alias = "MainDB",
            ConnectionString = "Server=localhost;",
            CliToolParameters = parameters
        };

    [Fact]
    public void ResolveCliToolArguments_ReplacesFilePath()
    {
        var tool = BuildTool("-i {FilePath} -b");
        var target = BuildTarget();

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/migrations/v1.sql");

        result.Should().Be("-i /migrations/v1.sql -b");
    }

    [Fact]
    public void ResolveCliToolArguments_ReplacesCustomParameter()
    {
        var tool = BuildTool("-S {Server} -U {User} -P {Password}");
        var target = BuildTarget(new Dictionary<string, string>
        {
            { "Server", "localhost" },
            { "User", "sa" },
            { "Password", "secret" }
        });

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/migrations/v1.sql");

        result.Should().Be("-S localhost -U sa -P secret");
    }

    [Fact]
    public void ResolveCliToolArguments_ReplacesFilePathAndCustomParameters()
    {
        var tool = BuildTool("-S {Server} -i {FilePath} -b");
        var target = BuildTarget(new Dictionary<string, string>
        {
            { "Server", "my-server" }
        });

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/migration.sql");

        result.Should().Be("-S my-server -i /tmp/migration.sql -b");
    }

    [Fact]
    public void ResolveCliToolArguments_NullCliToolParameters_OnlyFilePathReplaced()
    {
        var tool = BuildTool("-i {FilePath}");
        var target = BuildTarget(parameters: null);

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/file.sql");

        result.Should().Be("-i /tmp/file.sql");
    }

    [Fact]
    public void ResolveCliToolArguments_EmptyCliToolParameters_OnlyFilePathReplaced()
    {
        var tool = BuildTool("-i {FilePath}");
        var target = BuildTarget(parameters: new Dictionary<string, string>());

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/file.sql");

        result.Should().Be("-i /tmp/file.sql");
    }

    [Fact]
    public void ResolveCliToolArguments_UnknownPlaceholderInTemplate_RemainsUnchanged()
    {
        var tool = BuildTool("-S {Server} -d {Database}");
        var target = BuildTarget(new Dictionary<string, string>
        {
            { "Server", "localhost" }
            // "Database" not provided
        });

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/file.sql");

        result.Should().Be("-S localhost -d {Database}");
    }

    [Fact]
    public void ResolveCliToolArguments_ParameterValueWithSpaces_SubstitutedCorrectly()
    {
        var tool = BuildTool("-S {Server}");
        var target = BuildTarget(new Dictionary<string, string>
        {
            { "Server", "my server name" }
        });

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/file.sql");

        result.Should().Be("-S my server name");
    }

    [Fact]
    public void ResolveCliToolArguments_FilePathWithSpaces_SubstitutedCorrectly()
    {
        var tool = BuildTool("-i {FilePath}");
        var target = BuildTarget();

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/my migrations/Release 1.0/file.sql");

        result.Should().Be("-i /my migrations/Release 1.0/file.sql");
    }

    [Fact]
    public void ResolveCliToolArguments_NullParameterValue_ReplacedWithEmpty()
    {
        var tool = BuildTool("-P {Password}");
        var target = BuildTarget(new Dictionary<string, string>
        {
            { "Password", null! }
        });

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/file.sql");

        result.Should().Be("-P ");
    }

    [Fact]
    public void ResolveCliToolArguments_MultipleOccurrencesOfFilePath_AllReplaced()
    {
        var tool = BuildTool("-i {FilePath} --verify {FilePath}");
        var target = BuildTarget();

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/migration.sql");

        result.Should().Be("-i /tmp/migration.sql --verify /tmp/migration.sql");
    }

    [Fact]
    public void ResolveCliToolArguments_NoPlaceholders_TemplateReturnedUnchanged()
    {
        var tool = BuildTool("--help");
        var target = BuildTarget();

        var result = MigrationService.ResolveCliToolArguments(tool, target, "/tmp/file.sql");

        result.Should().Be("--help");
    }
}
