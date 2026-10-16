using System.Diagnostics;
using System.Text;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The CLI listens for Ctrl+C and SIGTERM while its command runs: a SIGINT sent to the real process in the middle of a
/// SQLite run cancels it, the run stops at its next block boundary and records why, and the process exits with 130. The
/// signal goes out when the console shows the end of the second file, so the run is in the seed file, whose second block
/// runs for seconds and which SQLite cannot interrupt. A process that the signal killed by default would exit with 130
/// as well but leave its run row open; the Cancelled row shows that the handler of the CLI took the signal. Linux and
/// macOS only: the test sends a POSIX signal.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteCliSignalTests : SqliteTestBase
{
    private const string Seed = "03_SeedDataA.sql";

    public SqliteCliSignalTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_SigIntWhileTheCommandRuns_RecordsTheRunAsCancelledAndExitsWith130()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The test sends SIGINT, a POSIX signal");

        // Arrange: the seed of release 1.0 runs for seconds in its second block; the console shows Information
        var config = Fixture.EngineConfig;
        await using var ctx = await CancellationContract.WithThreeBlockSeed(CreateScenario(), config, secondBlock: SqlDialect.GetSleepSql(config.DatabaseType, 4))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithSerilogMinimumLevel("Information")
            .BuildAsync();
        var output = new StringBuilder();
        var secondFileMigrated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cli = new Process
        {
            StartInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "raymigrator"))
            {
                ArgumentList = { "migrate-up", "-p", ScenarioBuilder.MainProductAlias, "-env", "Docker", "-tr", "Release_1.0", "-cd", ctx.WorkDirectory },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        cli.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (output) output.AppendLine(e.Data);
            if (e.Data.Contains("Migration successful", StringComparison.Ordinal) && e.Data.Contains("02_CreateTableB.sql", StringComparison.Ordinal))
            {
                secondFileMigrated.TrySetResult();
            }
        };
        cli.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (output) output.AppendLine(e.Data);
        };
        var stopwatch = Stopwatch.StartNew();
        cli.Start();
        cli.BeginOutputReadLine();
        cli.BeginErrorReadLine();

        // Act
        try
        {
            await secondFileMigrated.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            await SendSigIntAsync(cli.Id);
            await cli.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        }
        finally
        {
            if (!cli.HasExited) cli.Kill(entireProcessTree: true);
        }

        stopwatch.Stop();

        // Assert
        string console;
        lock (output) console = output.ToString();
        cli.ExitCode.Should().Be(130, because: $"the CLI exits like a process that SIGINT ended; console:\n{console}");
        ctx.GetMigrationRun(1)!.MigrationRunResultId.Should().Be((int)MigrationRunResult.Cancelled,
            because: "the handler of the CLI cancelled the run, which recorded why it stopped; a process that the signal killed leaves the run open");
        (ctx.GetMigrationRecord(Seed)?.MigrationStatusId ?? (int)MigrationStatus.NotMigrated).Should().NotBe((int)MigrationStatus.Migrated,
            because: "the run stopped before the seed file completed");
        console.Should().Contain("received: cancelling at the next block boundary", because: "the CLI warns where the command stops");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    private static async Task SendSigIntAsync(int processId)
    {
        using var kill = Process.Start(new ProcessStartInfo("kill") { ArgumentList = { "-INT", processId.ToString() }, UseShellExecute = false })!;
        await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
        kill.ExitCode.Should().Be(0, because: "kill delivers SIGINT to the CLI");
    }
}
