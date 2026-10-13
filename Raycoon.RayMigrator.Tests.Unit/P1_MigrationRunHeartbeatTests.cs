using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The run heartbeat (schema version 2): <see cref="MigrationRunHeartbeat"/> beats every interval until it is
/// stopped and survives a failing beat, the repository options carry the interval and the orphan threshold with
/// their defaults, every engine ships the heartbeat template and measures the heartbeat age in its orphan
/// select and fix templates, and <c>fix --older-than</c> defaults to the threshold.
/// </summary>
public class MigrationRunHeartbeatTests
{
    public static readonly TheoryData<string> Engines = new() { "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite" };

    [Fact]
    public async Task Heartbeat_BeatsUntilStopped()
    {
        int beats = 0;
        var logger = new CapturingLogger<MigrationRunHeartbeat>();

        await using (var heartbeat = new MigrationRunHeartbeat(7, TimeSpan.FromMilliseconds(40), () => { Interlocked.Increment(ref beats); return true; }, logger))
        {
            await Task.Delay(400, TestContext.Current.CancellationToken);
            heartbeat.Beats.Should().BeGreaterThan(2);
        }

        int afterStop = beats;
        await Task.Delay(150, TestContext.Current.CancellationToken);
        beats.Should().Be(afterStop, "a stopped heartbeat beats no more");
        logger.Entries.Should().NotContain(e => e.LogLevel == LogLevel.Warning);
    }

    [Fact]
    public async Task Heartbeat_FailingBeat_IsLoggedAndTheLoopContinues()
    {
        int calls = 0;
        var logger = new CapturingLogger<MigrationRunHeartbeat>();

        await using var heartbeat = new MigrationRunHeartbeat(7, TimeSpan.FromMilliseconds(40), () =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new InvalidOperationException("repository away");
            return true;
        }, logger);
        await Task.Delay(400, TestContext.Current.CancellationToken);

        calls.Should().BeGreaterThan(2, "the loop survives the failed beat");
        logger.Entries.Should().Contain(e => e.LogLevel == LogLevel.Warning && e.Message.Contains("MigrationRun 7"));
        heartbeat.Beats.Should().Be(calls - 1);
    }

    [Fact]
    public async Task Heartbeat_BeatOnAClosedRun_DoesNotCount()
    {
        await using var heartbeat = new MigrationRunHeartbeat(7, TimeSpan.FromMilliseconds(40), () => false, new CapturingLogger<MigrationRunHeartbeat>());
        await Task.Delay(200, TestContext.Current.CancellationToken);

        heartbeat.Beats.Should().Be(0);
    }

    [Fact]
    public async Task Heartbeat_Dispose_WithABeatThatHangs_ReturnsAfterTheStopTimeout()
    {
        var logger = new CapturingLogger<MigrationRunHeartbeat>();
        var release = new ManualResetEventSlim(false);
        var heartbeat = new MigrationRunHeartbeat(7, TimeSpan.FromMilliseconds(20), () => { release.Wait(); return true; }, logger, stopTimeout: TimeSpan.FromMilliseconds(200));
        await Task.Delay(100, TestContext.Current.CancellationToken); // the first beat is now blocked inside the repository call

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await heartbeat.DisposeAsync();
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "the stop waits for the stop timeout only, not for the beat");
        logger.Entries.Should().Contain(e => e.LogLevel == LogLevel.Warning && e.Message.Contains("still in progress"));
        release.Set();
    }

    [Fact]
    public void Heartbeat_DefaultStopTimeout_IsTenSeconds()
    {
        MigrationRunHeartbeat.DefaultStopTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Heartbeat_NonPositiveInterval_Throws()
    {
        var act = () => new MigrationRunHeartbeat(7, TimeSpan.Zero, () => true, new CapturingLogger<MigrationRunHeartbeat>());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RepositoryOptions_HeartbeatAndThreshold_HaveTheDocumentedDefaults()
    {
        DefaultOf(nameof(RepositoryOptions.HeartbeatIntervalInSeconds)).Should().Be(30);
        DefaultOf(nameof(RepositoryOptions.OrphanedRunThresholdInMinutes)).Should().Be(2);
        new RepositoryOptions().HeartbeatIntervalInSeconds.Should().BeNull("nullable, so the documented default is applied");
    }

    [Fact]
    public void FixIssuesRequest_OlderThan_DefaultsToTheOrphanThreshold()
    {
        new FixIssuesRequest().OlderThanMinutes.Should().Be(2);
    }

    [Fact]
    public async Task CommandLine_FixWithoutOlderThan_DefaultsToTheOrphanThreshold()
    {
        var config = new CommandLineConfiguration("RayMigrator Test");
        var parse = config.RootCommand.Parse(new[] { "fix", "-p", "P", "-env", "Dev" });
        await parse.InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.FixOlderThanMinutes.Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Templates_EveryEngine_ShipTheHeartbeatAndMeasureTheHeartbeatAge(string engine)
    {
        var heartbeat = ReadTemplate(engine, "Repository_MigrationRun_Heartbeat.sql");
        heartbeat.Should().Contain("TemplateType   = \"Repository_MigrationRun_Heartbeat\"").And.Contain("@MigrationRunId");

        ReadTemplate(engine, "Repository_MigrationRun_SelectOrphaned.sql").Should().Contain("MinutesSinceHeartbeat")
            .And.Contain("LastHeartbeatAt", "the select reports the heartbeat age the orphan rule compares");
        ReadTemplate(engine, "Repository_MigrationRun_FixOrphaned.sql").Should().Contain("@ThresholdMinutes",
            "the fix leaves a run alone whose heartbeat is younger than the threshold");
        ReadTemplate(engine, "DatabaseLogging_CheckCreate.sql").Should().Contain("115, 'TemplateExecutionRepositoryMigrationRunHeartbeat'");
    }

    #region Helpers

    private static int DefaultOf(string property)
    {
        var attribute = typeof(RepositoryOptions).GetProperty(property)!
            .GetCustomAttributes(typeof(RayRangeIntAttribute), false).Cast<RayRangeIntAttribute>().Single();
        // The attribute keeps the documented default in a private field and writes it into a null property at validation.
        var defaultField = typeof(RayRangeIntAttribute).GetField("_defaultValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return Convert.ToInt32(defaultField.GetValue(attribute));
    }

    private static string ReadTemplate(string engine, string templateFile)
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(assemblyDir, "DataAccessLayers", engine, templateFile);
        if (!File.Exists(path))
        {
            var dir = assemblyDir;
            while (dir != null && Directory.GetFiles(dir, "*.sln").Length == 0)
                dir = Directory.GetParent(dir)?.FullName;
            path = Path.Combine(dir ?? assemblyDir, $"Raycoon.RayMigrator.Database.{engine}", "Templates", templateFile);
        }
        return File.ReadAllText(path);
    }

    #endregion
}
