// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Testing;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.CliTool;

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "CliToolDocker")]
public class MariaDbCliToolDockerTests : MariaDbTestBase
{
    private const string TableName = "clitest_mariadb";

    public MariaDbCliToolDockerTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DockerExec_CreateTableAndInsert_DataExists()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var config = CliToolConfigHelper.GetStdinConfig(
            Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString);
        var resolvedArgs = DockerExecHelper.ResolveArguments(config.ArgumentTemplate, config.Parameters);

        var createAndInsertSql =
            $"CREATE TABLE {TableName} (Id INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(100));\n" +
            $"INSERT INTO {TableName} (Name) VALUES ('test');";

        try
        {
            var result = await DockerExecHelper.ExecuteViaStdinAsync(
                config.ExecutablePath, resolvedArgs, createAndInsertSql, config.TimeoutInSeconds);

            Assert.True(result.Success, $"Expected success but got ExitCode={result.ExitCode}, Stderr={result.Stderr}");
            Assert.Equal(0, result.ExitCode);

            var queryHelper = new RepositoryQueryHelper(
                Fixture.EngineConfig.DatabaseType,
                Fixture.EngineConfig.ConnectionString,
                Fixture.EngineConfig.SchemaName);

            int rowCount = queryHelper.CountRows(Fixture.EngineConfig.ConnectionString, TableName, useRepositorySchema: false);
            Assert.Equal(1, rowCount);
        }
        finally
        {
            var dropSql = $"DROP TABLE IF EXISTS {TableName};";
            await DockerExecHelper.ExecuteViaStdinAsync(
                config.ExecutablePath, resolvedArgs, dropSql, config.TimeoutInSeconds);
        }
    }

    [Fact]
    public async Task DockerExec_InvalidSql_ReturnsNonZeroExitCode()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var config = CliToolConfigHelper.GetStdinConfig(
            Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString);
        var resolvedArgs = DockerExecHelper.ResolveArguments(config.ArgumentTemplate, config.Parameters);

        var result = await DockerExecHelper.ExecuteViaStdinAsync(
            config.ExecutablePath, resolvedArgs,
            "SELECT * FROM nonexistent_table_xyz_12345;",
            config.TimeoutInSeconds);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task DockerExec_EmptyInput_Succeeds()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var config = CliToolConfigHelper.GetStdinConfig(
            Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString);
        var resolvedArgs = DockerExecHelper.ResolveArguments(config.ArgumentTemplate, config.Parameters);

        var result = await DockerExecHelper.ExecuteViaStdinAsync(
            config.ExecutablePath, resolvedArgs, "", config.TimeoutInSeconds);

        Assert.Equal(0, result.ExitCode);
    }
}
