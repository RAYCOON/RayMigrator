using AwesomeAssertions;
using Raycoon.RayMigrator.Shared.Constants;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The run lock under starts at the same moment: hosts that start a run of one product and environment at once take
/// the run lock once. <c>Repository_MigrationRun_Insert</c> checks for an open run of the product and environment and
/// inserts the run and its MigrationRunMeta row under the serialization of the engine: <c>UPDLOCK, HOLDLOCK</c> in a
/// transaction on SQL Server, <c>pg_advisory_xact_lock</c> on PostgreSQL, <c>GET_LOCK</c> on MariaDB and MySQL and a
/// <c>BEGIN IMMEDIATE</c> transaction on SQLite. Exactly one start gets a run; every other start is refused with -2,
/// which the engine turns into <c>MigrationAlreadyRunningException</c>. The starts run the template of the engine on
/// DALs of their own, as the processes of several hosts do.
/// </summary>
internal static class RunLockContract
{
    /// <summary>How many hosts start a run at the same moment in each round.</summary>
    private const int ParallelStarts = 8;

    /// <summary>How many rounds of parallel starts run: the race of two starts is narrow.</summary>
    private const int Rounds = 10;

    /// <summary>
    /// After a migrate-up registered the product, the environment and the engine version, each round lets
    /// <see cref="ParallelStarts"/> starts of their own, each on a DAL and a connection of its own, run the run insert
    /// at once: exactly one gets a run, the others are refused with -2, one run is open and every run has its
    /// MigrationRunMeta row. The round closes the run of each start that got one through the orphan fix, so that the
    /// next round starts without an open run.
    /// </summary>
    public static async Task VerifyParallelStartsTakeTheRunLockOnceAsync(ScenarioBuilder builder)
    {
        // Arrange: the migrate-up registered the product, the environment and the engine version, and its run is finished
        await using var ctx = await builder.BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        var failedStarts = new List<string>();
        var findings = new List<string>();
        int surplusRuns = 0;

        // Act
        for (int round = 1; round <= Rounds; round++)
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = Enumerable.Range(0, ParallelStarts).Select(_ => ctx.InsertMigrationRunWithNewDalAsync(start.Task)).ToList();
            start.SetResult();

            var runIds = new List<int>();
            int refused = 0;
            foreach (var task in starts)
            {
                try
                {
                    int code = await task;
                    if (code > 0)
                        runIds.Add(code);
                    else if (code == TemplateResultCode.MigrationAlreadyRunning)
                        refused++;
                    else
                        findings.Add($"round {round}: a start returned {code}");
                }
                catch (Exception ex)
                {
                    var cause = ex.GetBaseException();
                    failedStarts.Add($"round {round}: {cause.GetType().Name}: {cause.Message}");
                }
            }

            int open = ctx.CountRunningMigrationRuns();
            int runs = ctx.CountMigrationRuns();
            int runMeta = ctx.CountRepoRows("MigrationRunMeta");
            if (runIds.Count != 1 || refused != ParallelStarts - 1 || open != 1 || runMeta != runs)
            {
                findings.Add($"round {round}: {runIds.Count} start(s) took the run lock [{string.Join(", ", runIds)}], {refused} were refused, " +
                             $"{open} open run(s), {runMeta} MigrationRunMeta rows for {runs} runs");
            }

            surplusRuns += Math.Max(0, runIds.Count - 1);

            // Every run a start got is closed, so that the next round starts without an open run
            foreach (int runId in runIds)
                await ctx.FixOrphanedRunAsync(runId, 0);
        }

        // Assert
        failedStarts.Should().BeEmpty(because: "every start takes the run lock or is refused, but {0} of {1} parallel starts failed",
            failedStarts.Count, Rounds * ParallelStarts);
        findings.Should().BeEmpty(
            because: "in every round exactly one of {0} starts takes the run lock with its run and its MigrationRunMeta row, and the others are refused with -2, but {1} of {2} rounds broke it with {3} runs too many ({4})",
            ParallelStarts, findings.Count, Rounds, surplusRuns, string.Join("; ", findings));
    }
}
