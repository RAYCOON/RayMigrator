
using FluentAssertions;
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
/// P2: Tests for RayMigratorDatabaseSink.Emit() run-mode filter.
/// Verifies that database logging only occurs in Migrate mode (RunModeId = 100).
/// Early-pipeline logs without RunModeId (null) pass through.
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

    #region RunModeId Filter — should enqueue

    [Fact]
    public void Emit_WithRunModeIdMigrate_EnqueuesLogEntry()
    {
        // Arrange
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("RunModeId", (byte)MigrationRunMode.Migrate));

        // Act
        sink.Emit(logEvent);

        // Assert — DAL is called, proving the log entry was not filtered out
        WaitForCondition(() => dalCalled).Should().BeTrue(
            "RunModeId = Migrate (100) must pass the run-mode filter and reach the DAL");
    }

    [Fact]
    public void Emit_WithoutRunModeId_EnqueuesLogEntry()
    {
        // Arrange — no RunModeId property simulates early-pipeline logs
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(LogEventLevel.Information
            /* no RunModeId property */);

        // Act
        sink.Emit(logEvent);

        // Assert — null RunModeId is treated as an early-pipeline log and must pass through
        WaitForCondition(() => dalCalled).Should().BeTrue(
            "logs without RunModeId (null) must pass through to capture early pipeline context");
    }

    #endregion

    #region RunModeId Filter — should NOT enqueue

    [Fact]
    public void Emit_WithRunModeIdSimulate_DoesNotEnqueueLogEntry()
    {
        // Arrange
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("RunModeId", (byte)MigrationRunMode.Simulate));

        // Act
        sink.Emit(logEvent);

        // Give the background queue a brief window — if the filter is broken the DAL would be called
        Thread.Sleep(200);

        // Assert — Simulate mode must be silently dropped
        dalCalled.Should().BeFalse(
            "RunModeId = Simulate (20) must be filtered out; DB logging is only allowed in Migrate mode");
    }

    [Fact]
    public void Emit_WithRunModeIdValidate_DoesNotEnqueueLogEntry()
    {
        // Arrange
        var (sink, dal) = CreateInitializedSink();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

        var logEvent = CreateLogEvent(
            LogEventLevel.Information,
            ("RunModeId", (byte)MigrationRunMode.Validate));

        // Act
        sink.Emit(logEvent);

        Thread.Sleep(200);

        // Assert — Validate mode must be silently dropped
        dalCalled.Should().BeFalse(
            "RunModeId = Validate (10) must be filtered out; DB logging is only allowed in Migrate mode");
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
            ("RunModeId", (byte)MigrationRunMode.Migrate)); // correct run mode, wrong level

        // Act
        sink.Emit(logEvent);

        Thread.Sleep(200);

        // Assert — minimum-level guard must still reject the entry
        dalCalled.Should().BeFalse(
            "a log event below the sink's minimum level must be dropped even when RunModeId = Migrate");
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
            ("RunModeId", (byte)MigrationRunMode.Migrate));

        // Act & Assert — must not throw even when no writer is attached
        var act = () => sink.Emit(logEvent);
        act.Should().NotThrow();
    }

    #endregion
}
