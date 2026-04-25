
using FluentAssertions;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.CliTool;

/// <summary>
/// Verifies that Docker presets in CliToolPresetProvider are structurally consistent
/// with the test configurations in CliToolConfigHelper. No Docker required.
/// </summary>
[Trait("Category", "CliTool")]
public class CliToolPresetConsistencyTests
{
    private static readonly Dictionary<string, string> ContainerNames = new()
    {
        ["sqlcmd-docker"] = "rm_db_sqlserver",
        ["psql-docker"] = "rm_db_postgresql",
        ["mariadb-docker"] = "rm_db_mariadb",
        ["mysql-docker"] = "rm_db_mysql"
    };

    [Theory]
    [InlineData("sqlcmd-docker", "SqlServer")]
    [InlineData("psql-docker", "PostgreSQL")]
    [InlineData("mariadb-docker", "MariaDb")]
    [InlineData("mysql-docker", "MySql")]
    public void DockerPreset_ResolvedArgumentTemplate_MatchesTestConfig(string presetAlias, string databaseType)
    {
        var preset = CliToolPresetProvider.GetPresetByAlias(presetAlias);
        preset.Should().NotBeNull($"preset '{presetAlias}' should exist in CliToolPresetProvider");

        string containerName = ContainerNames[presetAlias];
        string resolvedPresetTemplate = preset!.ArgumentTemplate.Replace("{ContainerName}", containerName);

        // Use a dummy connection string -- CliToolConfigHelper extracts user/password/database from it,
        // but we only compare the argument template structure (which has {User}, {Password}, {Database} placeholders)
        var testConfig = CliToolConfigHelper.GetStdinConfig(databaseType, "");

        testConfig.ArgumentTemplate.Should().Be(resolvedPresetTemplate,
            $"CliToolConfigHelper's test config for {databaseType} should match the resolved Docker preset '{presetAlias}'");
    }

    [Theory]
    [InlineData("sqlcmd-docker", "SqlServer")]
    [InlineData("psql-docker", "PostgreSQL")]
    [InlineData("mariadb-docker", "MariaDb")]
    [InlineData("mysql-docker", "MySql")]
    public void DockerPreset_InputMode_IsStdin(string presetAlias, string databaseType)
    {
        var preset = CliToolPresetProvider.GetPresetByAlias(presetAlias);
        preset.Should().NotBeNull();

        preset!.InputMode.Should().Be("Stdin",
            $"Docker preset '{presetAlias}' should use Stdin mode");

        var testConfig = CliToolConfigHelper.GetStdinConfig(databaseType, "");
        testConfig.InputMode.Should().Be("Stdin",
            $"Test config for {databaseType} should also use Stdin mode");
    }

    [Theory]
    [InlineData("sqlcmd-docker", "SqlServer")]
    [InlineData("psql-docker", "PostgreSQL")]
    [InlineData("mariadb-docker", "MariaDb")]
    [InlineData("mysql-docker", "MySql")]
    public void DockerPreset_ExecutablePath_IsDocker(string presetAlias, string databaseType)
    {
        var preset = CliToolPresetProvider.GetPresetByAlias(presetAlias);
        preset.Should().NotBeNull();

        preset!.ExecutablePath.Should().Be("docker",
            $"Docker preset '{presetAlias}' should use 'docker' as executable");

        var testConfig = CliToolConfigHelper.GetStdinConfig(databaseType, "");
        testConfig.ExecutablePath.Should().Be("docker",
            $"Test config for {databaseType} should also use 'docker' as executable");
    }

    [Theory]
    [InlineData("sqlcmd-docker", "SqlServer")]
    [InlineData("psql-docker", "PostgreSQL")]
    [InlineData("mariadb-docker", "MariaDb")]
    [InlineData("mysql-docker", "MySql")]
    public void DockerPreset_Timeout_MatchesTestConfig(string presetAlias, string databaseType)
    {
        var preset = CliToolPresetProvider.GetPresetByAlias(presetAlias);
        preset.Should().NotBeNull();

        var testConfig = CliToolConfigHelper.GetStdinConfig(databaseType, "");

        preset!.CliToolTimeoutInSeconds.Should().Be(testConfig.TimeoutInSeconds,
            $"Docker preset '{presetAlias}' timeout should match test config for {databaseType}");
    }
}
