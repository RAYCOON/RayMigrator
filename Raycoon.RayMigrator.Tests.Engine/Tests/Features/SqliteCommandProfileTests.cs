using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// #6: side effects follow the command profile, not a hidden run mode.
/// Read-only commands start against an unreachable target, leave no audit-log rows and no product/environment
/// rows behind; state-changing commands log; baseline runs are listed as <see cref="MigrationOperation.Baseline"/>.
/// Sqlite is enough: the profile is database-independent.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteCommandProfileTests : SqliteTestBase
{
    public SqliteCommandProfileTests(SqliteFixture fixture) : base(fixture) { }

    /// <summary>A Sqlite file in a directory that does not exist: building the connection works, opening it fails.</summary>
    private static string UnreachableTarget()
        => $"Data Source={Path.Combine(Path.GetTempPath(), "RayMigrator_DoesNotExist_" + Guid.NewGuid().ToString("N"), "unreachable.sqlite")}";

    /// <summary>Flushes the asynchronous DatabaseLogWriter queue and returns the MigrationLog row count.</summary>
    private static int FlushedLogRows(ScenarioContext ctx)
    {
        ctx.FlushDatabaseLog();
        return ctx.CountLogEntries();
    }

    #region Reproduction A: unreachable target

    [Fact]
    public async Task Info_WithUnreachableTarget_Succeeds()
    {
        await using var ctx = await CreateScenario()
            .WithTargetConnectionString(UnreachableTarget())
            .BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        var result = await ctx.InfoAsync();

        result.TotalMigrationsExecuted.Should().Be(0);
        result.PendingMigrations.Should().BeGreaterThan(0, "info never touches a target, so an unreachable one must not stop it");
    }

    [Fact]
    public async Task ValidateHash_WithUnreachableTarget_Succeeds()
    {
        await using var ctx = await CreateScenario()
            .WithTargetConnectionString(UnreachableTarget())
            .BuildAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);

        var result = await ctx.ValidateHashAsync();

        result.Success.Should().BeTrue($"validate-hash only reads the repository: {result.ErrorMessage}");
        result.InvalidFiles.Should().Be(0);
        result.MissingFiles.Should().Be(0);
    }

    [Fact]
    public async Task UpdateHash_WithUnreachableTarget_Succeeds()
    {
        await using var ctx = await CreateScenario()
            .WithTargetConnectionString(UnreachableTarget())
            .BuildAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);

        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue($"update-hash only writes the repository: {result.ErrorMessage}");
    }

    [Fact]
    public async Task MigrateUp_Simulate_WithUnreachableTarget_Aborts()
    {
        // Control: Simulate is documented to connect to the targets, so the start-up check must still abort.
        var act = async () => await CreateScenario()
            .WithTargetConnectionString(UnreachableTarget())
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);

        await act.Should().ThrowAsync<ApplicationStartupException>()
            .WithMessage("*Failed to validate or connect to DatabaseType [Sqlite]*");
    }

    [Fact]
    public async Task MigrateUp_Migrate_WithUnreachableTarget_Aborts()
    {
        var act = async () => await CreateScenario()
            .WithTargetConnectionString(UnreachableTarget())
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);

        await act.Should().ThrowAsync<ApplicationStartupException>()
            .WithMessage("*Failed to validate or connect to DatabaseType [Sqlite]*");
    }

    [Fact]
    public async Task MigrateUp_Validate_WithUnreachableTarget_Runs()
    {
        await using var ctx = await CreateScenario()
            .WithTargetConnectionString(UnreachableTarget())
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Validate);

        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Validate);

        result.Success.Should().BeTrue($"Validate mode never opens a connection: {result.ErrorMessage}");
    }

    #endregion

    #region Reproduction B: audit log follows the command

    // Serilog's global minimum is lowered to Information in these scenarios so that the commands' own
    // Information events reach the sink; otherwise "0 rows" would only prove the level filter.

    [Fact]
    public async Task Info_WithDatabaseLogging_WritesNoLogRows()
    {
        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Information")
            .WithDatabaseLogging("Information")
            .BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        await ctx.InfoAsync();

        FlushedLogRows(ctx).Should().Be(0, "a read-only status query must not fill the audit log");
    }

    [Fact]
    public async Task ValidateHash_WithDatabaseLogging_WritesNoLogRows()
    {
        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Information")
            .WithDatabaseLogging("Information")
            .BuildAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);

        await ctx.ValidateHashAsync();

        FlushedLogRows(ctx).Should().Be(0, "validate-hash changes nothing and leaves no audit trail");
    }

    [Fact]
    public async Task FixDryRun_WithDatabaseLogging_WritesNoLogRows()
    {
        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Information")
            .WithDatabaseLogging("Information")
            .BuildAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate, fixDryRun: true);

        var result = await ctx.FixIssuesAsync(dryRun: true);

        result.Success.Should().BeTrue(result.ErrorMessage);
        FlushedLogRows(ctx).Should().Be(0, "fix --dry-run changes nothing and leaves no audit trail");
    }

    [Fact]
    public async Task UpdateHash_WithDatabaseLogging_WritesLogRows()
    {
        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Information")
            .WithDatabaseLogging("Information")
            .BuildAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);

        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue(result.ErrorMessage);
        FlushedLogRows(ctx).Should().BeGreaterThan(0, "update-hash changes repository state and must be audited");
    }

    [Fact]
    public async Task Baseline_WithDatabaseLogging_WritesLogRows()
    {
        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Information")
            .WithDatabaseLogging("Information")
            .BuildAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate);

        var result = await ctx.BaselineAsync();

        result.Success.Should().BeTrue(result.ErrorMessage);
        FlushedLogRows(ctx).Should().BeGreaterThan(0, "baseline writes MigrationRun/MigrationRecord rows and must be audited");
    }

    #endregion

    #region Baseline is named after what it did

    [Fact]
    public async Task Baseline_ThenInfo_HistoryShowsBaselineOperation()
    {
        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate);

        var baseline = await ctx.BaselineAsync();
        baseline.Success.Should().BeTrue(baseline.ErrorMessage);
        baseline.BaselinedFiles.Should().BeGreaterThan(0);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var history = await ctx.GetHistoryAsync();

        history.Runs.Should().HaveCount(1);
        history.Runs[0].Operation.Should().Be(MigrationOperation.Baseline, "a baseline run is not an up-migration");
        history.Runs[0].RunMode.Should().Be(MigrationRunMode.Migrate, "MigrationRunModeId stays 100 for anything that writes records (#5)");
        history.Runs[0].TotalMigrations.Should().Be(baseline.BaselinedFiles);
    }

    [Fact]
    public async Task MigrateUp_ThenInfo_HistoryStillShowsMigrateUp()
    {
        await using var ctx = await CreateScenario().BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var history = await ctx.GetHistoryAsync();

        history.Runs.Should().HaveCount(1);
        history.Runs[0].Operation.Should().Be(MigrationOperation.MigrateUp);
    }

    #endregion

    #region Read-only commands leave no bookkeeping rows

    [Fact]
    public async Task Info_OnFreshRepository_DoesNotInsertProduct()
    {
        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        var result = await ctx.InfoAsync();

        result.TotalMigrationsExecuted.Should().Be(0);
        result.PendingMigrations.Should().BeGreaterThan(0);
        ctx.GetProductId().Should().Be(-1, "a read-only command must not register the product as a side effect");
    }

    [Fact]
    public async Task ValidateHash_OnFreshRepository_DoesNotInsertProduct()
    {
        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);

        var result = await ctx.ValidateHashAsync();

        result.Success.Should().BeTrue(result.ErrorMessage);
        ctx.GetProductId().Should().Be(-1, "a read-only command must not register the product as a side effect");
    }

    [Fact]
    public async Task UpdateHash_OnFreshRepository_InsertsProduct()
    {
        // Control: a state-changing command still registers product and environment.
        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);

        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue(result.ErrorMessage);
        ctx.GetProductId().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Info_AfterMigrateUp_ReadsRecordsWithoutWriting()
    {
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        int productId = ctx.GetProductId();

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.PendingMigrations.Should().Be(0);
        result.TotalMigrationsExecuted.Should().BeGreaterThan(0, "the read-only lookup resolves the existing product and environment");
        ctx.GetProductId().Should().Be(productId);
    }

    #endregion

    #region Request and context agree on the run mode

    [Fact]
    public async Task MigrateUp_WithExplicitRunMode_RebuildsHostToMatch()
    {
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);

        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        result.Success.Should().BeTrue(result.ErrorMessage);
        ctx.HostConsoleOptions.RunMode.Should().Be(MigrationRunMode.Simulate, "the context must carry the run mode the request ran in");
        ctx.GetProductId().Should().Be(-1, "a Simulate run writes nothing, not even the product row");
    }

    #endregion
}
