// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbRepositoryIntegrityTests : MariaDbTestBase
{
    public MariaDbRepositoryIntegrityTests(MariaDbFixture fixture) : base(fixture) { }

    /// <summary>
    /// R1: After MigrateUp, all core repository tables should exist.
    /// </summary>
    [Fact]
    public async Task AllRepoTables_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        string[] repoTables = ["Product", "Environment", "MigrationRun", "MigrationRecord", "MigratorMeta"];
        foreach (string table in repoTables)
        {
            ctx.AssertRepositoryTableExists(table, true);
        }
    }

    /// <summary>
    /// R2: After MigrateUp, the Product record should exist in the repository.
    /// </summary>
    [Fact]
    public async Task Product_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        ctx.AssertProductExists(true);
    }

    /// <summary>
    /// R4: After MigrateUp, the Environment record should exist in the repository.
    /// </summary>
    [Fact]
    public async Task Environment_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        ctx.AssertEnvironmentExists("Docker", true);
    }

    /// <summary>
    /// R3: After MigrateUp, Migration records should exist and the latest run should be Ok.
    /// </summary>
    [Fact]
    public async Task MigrationRecords_ShouldBeMigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        ctx.CountMigrations().Should().BeGreaterThan(0, "Migration records should exist in repository");
        ctx.CountMigrationRuns().Should().BeGreaterThan(0, "At least one MigrationRun should exist");
        ctx.GetLatestRunResultId().Should().Be((int)MigrationRunResult.Ok,
            "Latest MigrationRun should have Result=Ok");
    }
}
