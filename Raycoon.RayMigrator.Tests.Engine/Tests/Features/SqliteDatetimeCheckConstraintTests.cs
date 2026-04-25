
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// P2: Engine tests for DAL-021's strict ISO-8601 CHECK constraints on SQLite repository datetime columns.
///
/// Verifies:
///   - Bad datetime input (e.g. 'yesterday') is rejected by the strict CHECK.
///   - Valid datetime('now') input is accepted.
///   - NULL values on nullable datetime columns pass the null-tolerant CHECK
///     (indirectly covered by MigrateUpAsync, which writes rows with NULL FinishedAt).
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteDatetimeCheckConstraintTests : SqliteTestBase
{
    private static readonly DalSettings Settings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 10,
        MaxRetries = 0,
        RetryDelayMs = 0,
    };

    public SqliteDatetimeCheckConstraintTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task BadDatetimeInProduct_IsRejectedByCheckConstraint()
    {
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        DalFactory.TryGetDal("Sqlite", Fixture.EngineConfig.ConnectionString, out IDal? dal)
            .Should().BeTrue("the SQLite DAL must be discoverable by DalFactory");

        Func<Task> act = () => dal!.ExecuteNonQueryAsync(
            "INSERT INTO \"Product\" (\"Name\",\"NameLower\",\"CreatedAt\") VALUES ('Bad','bad','yesterday');",
            Settings);

        var ex = await act.Should().ThrowAsync<Exception>();
        ex.And.Message.Should().Contain("CHECK constraint failed",
            "DAL-021: strict CHECK (datetime(\"CreatedAt\") IS NOT NULL AND datetime(\"CreatedAt\") = \"CreatedAt\") must reject 'yesterday'");
    }

    [Fact]
    public async Task ValidDatetimeInProduct_IsAccepted()
    {
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        DalFactory.TryGetDal("Sqlite", Fixture.EngineConfig.ConnectionString, out IDal? dal)
            .Should().BeTrue();

        await dal!.ExecuteNonQueryAsync(
            "INSERT INTO \"Product\" (\"Name\",\"NameLower\",\"CreatedAt\") VALUES ('Good','good',datetime('now'));",
            Settings);

        var count = await dal.ExecuteScalarAsync(
            "SELECT COUNT(*) FROM \"Product\" WHERE \"Name\"='Good';",
            Settings);

        Convert.ToInt32(count).Should().Be(1,
            "DAL-021: an INSERT using datetime('now') must pass the strict CHECK");
    }

    [Fact]
    public async Task NullFinishedAtInMigrationRun_IsAccepted()
    {
        // Indirect coverage: the migration pipeline writes rows into MigrationRun with NULL FinishedAt
        // during interrupted-run handling. If the null-tolerant CHECK were wrong, MigrateUp would fail.
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.CountMigrationRuns().Should().BeGreaterThan(0,
            "DAL-021: null-tolerant CHECK (\"X\" IS NULL OR ...) must allow NULL FinishedAt values");
    }
}
