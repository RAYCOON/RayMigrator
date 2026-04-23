// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class CliToolPresetProviderTests
{
    [Fact]
    public void GetAllPresets_Returns10Presets()
    {
        CliToolPresetProvider.GetAllPresets().Should().HaveCount(10);
    }

    [Fact]
    public void GetAllPresets_AllHaveRequiredFields()
    {
        foreach (var preset in CliToolPresetProvider.GetAllPresets())
        {
            preset.Alias.Should().NotBeNullOrWhiteSpace();
            preset.DatabaseType.Should().NotBeNullOrWhiteSpace();
            preset.ExecutablePath.Should().NotBeNullOrWhiteSpace();
            preset.ArgumentTemplate.Should().NotBeNullOrWhiteSpace();
            preset.Description.Should().NotBeNullOrWhiteSpace();
            preset.SuccessExitCodes.Should().NotBeEmpty();
            preset.CliToolTimeoutInSeconds.Should().BeGreaterThan(0);
            preset.ExpectedParameterKeys.Should().NotBeEmpty();
        }
    }

    [Theory]
    [InlineData("SqlServer", 2)]
    [InlineData("PostgreSQL", 2)]
    [InlineData("MariaDb", 2)]
    [InlineData("MySql", 2)]
    [InlineData("Sqlite", 2)]
    public void GetPresetsForDatabaseType_Returns2PerType(string dbType, int expected)
    {
        CliToolPresetProvider.GetPresetsForDatabaseType(dbType).Should().HaveCount(expected);
    }

    [Fact]
    public void GetDockerPresets_Returns5()
    {
        CliToolPresetProvider.GetDockerPresets().Should().HaveCount(5);
    }

    [Fact]
    public void GetDockerPresets_AllAreDockerVariants()
    {
        foreach (var preset in CliToolPresetProvider.GetDockerPresets())
        {
            preset.IsDockerVariant.Should().BeTrue();
            preset.ExecutablePath.Should().Be("docker");
        }
    }

    [Fact]
    public void GetPresetByAlias_ExistingAlias_ReturnsPreset()
    {
        CliToolPresetProvider.GetPresetByAlias("sqlcmd").Should().NotBeNull();
        CliToolPresetProvider.GetPresetByAlias("psql-docker").Should().NotBeNull();
    }

    [Fact]
    public void GetPresetByAlias_UnknownAlias_ReturnsNull()
    {
        CliToolPresetProvider.GetPresetByAlias("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetPresetByAlias_CaseInsensitive()
    {
        CliToolPresetProvider.GetPresetByAlias("SQLCMD").Should().NotBeNull();
    }

    [Fact]
    public void NativePresets_HaveCorrectInputMode()
    {
        // sqlcmd, psql, sqlite3 use File mode
        CliToolPresetProvider.GetPresetByAlias("sqlcmd")!.InputMode.Should().Be("File");
        CliToolPresetProvider.GetPresetByAlias("psql")!.InputMode.Should().Be("File");
        CliToolPresetProvider.GetPresetByAlias("sqlite3")!.InputMode.Should().Be("File");

        // mariadb, mysql use Stdin mode
        CliToolPresetProvider.GetPresetByAlias("mariadb")!.InputMode.Should().Be("Stdin");
        CliToolPresetProvider.GetPresetByAlias("mysql")!.InputMode.Should().Be("Stdin");
    }

    [Fact]
    public void DockerPresets_AllUseStdinMode()
    {
        foreach (var preset in CliToolPresetProvider.GetDockerPresets())
        {
            preset.InputMode.Should().Be("Stdin");
        }
    }
}
