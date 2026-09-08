using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for RayMigratorDatabaseSink.Emit() gate.
/// Database logging follows the command profile: MigrationContextEnricher emits <c>DbLogEnabled</c>
/// (CommandProfile.WritesDatabaseLog) and the sink drops every event that carries <c>DbLogEnabled = false</c> (#6).
/// <c>RunModeId</c> is still stored in the log row but no longer decides anything.
/// Early-pipeline logs without the property (null) pass through.
/// </summary>
public class RayMigratorDatabaseSinkTests
{
    #region Helper Methods

    private static (RayMigratorDatabaseSink sink, IDal dal) CreateInitializedSink()
    {
        var dal = Substitute.For<IDal>();

        var options = new RayMigratorOptions
        {
            DatabaseLogging = new DatabaseLoggingOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=test",
                SchemaName = "logs",
                MinimumLevel = "Debug",
                DbCommandTimeoutInSeconds = 20
            }
        };

        var writer = new DatabaseLogWriter(options, dal);

        // Bypass InitDatabaseLogger (requires a real DB) by setting state via reflection
        var initializedField = typeof(DatabaseLogWriter).GetField(
            "_isDatabaseLoggingInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        initializedField!.SetValue(writer, true);

        var templateField = typeof(DatabaseLogWriter).GetField(
            "_templateLoggingInsert",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        templateField!.SetValue(writer, new Raycoon.RayMigrator.Core.Templates.Template { Content = "INSERT INTO logs" });

        var sink = new RayMigratorDatabaseSink(writer, LogEventLevel.Debug);
        return (sink, dal);
    }

    private static LogEvent CreateLogEvent(
        LogEventLevel level = LogEventLevel.Information,
        params (string name, object? value)[] properties)
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            null,
            new MessageTemplateParser().Parse("Test message"),
            Enumerable.Empty<LogEventProperty>());

        foreach (var (name, value) in properties)
        {
            logEvent.AddPropertyIfAbsent(new LogEventProperty(name, new ScalarValue(value)));
        }

        return logEvent;
    }

    /// <summary>
    /// Polls a condition until it becomes true or timeout (5 s) is reached.
    /// Avoids Thread.Sleep with a fixed duration, which is unreliable on CI runners.
    /// </summary>
    private static bool WaitForCondition(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
            Thread.Sleep(50);
        return condition();
    }

    #endregion

    #region DbLogEnabled gate — should enqueue

    [Fact]
    public void Emit_WithDbLogEnabledTrue_EnqueuesLogEntry()
    {
        // Arrange — what the enricher emits for update-hash, baseline, fix and migrate-up/-down in Migrate mode
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("DbLogEnabled", true),
            ("RunModeId", (byte)MigrationRunMode.Migrate));

        // Act
        sink.Emit(logEvent);

        // Assert — DAL is called, proving the log entry was not filtered out
        WaitForCondition(() => dalCalled).Should().BeTrue(
            "DbLogEnabled = true must pass the gate and reach the DAL");
    }

    [Fact]
    public void Emit_WithoutDbLogEnabled_EnqueuesLogEntry()
    {
        // Arrange — no DbLogEnabled/RunModeId property simulates early-pipeline logs
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(LogEventLevel.Information
            /* no DbLogEnabled property */);

        // Act
        sink.Emit(logEvent);

        // Assert — a missing DbLogEnabled is treated as an early-pipeline log and must pass through
        WaitForCondition(() => dalCalled).Should().BeTrue(
            "logs without DbLogEnabled (null) must pass through to capture early pipeline context");
    }

    [Fact]
    public void Emit_WithRunModeIdSimulate_ButNoDbLogEnabled_EnqueuesLogEntry()
    {
        // Arrange — RunModeId alone no longer gates anything; only the enricher's DbLogEnabled does (#6)
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("RunModeId", (byte)MigrationRunMode.Simulate));

        // Act
        sink.Emit(logEvent);

        // Assert
        WaitForCondition(() => dalCalled).Should().BeTrue(
            "RunModeId is a stamp, not a gate; without DbLogEnabled the event passes through");
    }

    #endregion

    #region DbLogEnabled gate — should NOT enqueue

    [Theory]
    [InlineData((byte)MigrationRunMode.Migrate)]
    [InlineData((byte)MigrationRunMode.Simulate)]
    [InlineData((byte)MigrationRunMode.Validate)]
    public void Emit_WithDbLogEnabledFalse_DoesNotEnqueueLogEntry(byte runModeId)
    {
        // Arrange — what the enricher emits for info, validate-hash, fix --dry-run, simulate and validate runs.
        // Migrate + false is the info/validate-hash case: they run in Migrate mode and must still stay silent.
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("DbLogEnabled", false),
            ("RunModeId", runModeId));

        // Act
        sink.Emit(logEvent);

        // Give the background queue a brief window — if the gate is broken the DAL would be called
        Thread.Sleep(200);

        // Assert
        dalCalled.Should().BeFalse(
            $"DbLogEnabled = false must be silently dropped regardless of RunModeId ({runModeId})");
    }

    #endregion

    #region Minimum Level Filter — existing guard still works alongside run-mode filter

    [Fact]
    public void Emit_BelowMinimumLevel_DoesNotEnqueueLogEntry()
    {
        // Arrange — sink minimum level is Debug; emit Verbose
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(
            LogEventLevel.Verbose,
            ("DbLogEnabled", true),
            ("RunModeId", (byte)MigrationRunMode.Migrate)); // gate open, wrong level

        // Act
        sink.Emit(logEvent);

        Thread.Sleep(200);

        // Assert — minimum-level guard must still reject the entry
        dalCalled.Should().BeFalse(
            "a log event below the sink's minimum level must be dropped even when DbLogEnabled = true");
    }

    #endregion

    #region Deferred Writer — SetWriter after construction

    [Fact]
    public void Emit_WithDeferredWriter_NotInitialized_DoesNotThrow()
    {
        // Arrange — deferred constructor, writer never set
        var sink = new RayMigratorDatabaseSink(LogEventLevel.Debug);
        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("DbLogEnabled", true),
            ("RunModeId", (byte)MigrationRunMode.Migrate));

        // Act & Assert — must not throw even when no writer is attached
        var act = () => sink.Emit(logEvent);
        act.Should().NotThrow();
    }

    #endregion

    #region EventId

    [Fact]
    public void Emit_WithSerilogEventIdStructure_PassesEventIdToWriter()
    {
        // Arrange — Serilog.Extensions.Logging attaches the Microsoft EventId as one property named "EventId"
        // whose value is a StructureValue { Id, Name }. That is the only shape the sink will ever see.
        var (sink, dal) = CreateInitializedSink();
        DalParameterList? captured = null;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(call => captured = call.Arg<DalParameterList>());

        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("DbLogEnabled", true),
            ("RunModeId", (byte)MigrationRunMode.Migrate));
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunInsert;
        logEvent.AddPropertyIfAbsent(new LogEventProperty("EventId", new StructureValue(new[]
        {
            new LogEventProperty("Id", new ScalarValue(eventId.Id)),
            new LogEventProperty("Name", new ScalarValue(eventId.Name))
        })));

        // Act
        sink.Emit(logEvent);

        // Assert
        WaitForCondition(() => captured != null).Should().BeTrue("the event must reach the DAL");
        captured!.TryGetValue("MigrationEventId", out var parameter).Should().BeTrue();
        parameter!.ParameterValue.Should().Be(eventId.Id,
            "the EventId of the logger call must be written to MigrationLog.MigrationEventId, not 0");
    }

    #endregion
}
