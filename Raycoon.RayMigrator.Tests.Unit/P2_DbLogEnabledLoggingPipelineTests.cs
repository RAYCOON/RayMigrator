using AwesomeAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Raycoon.RayMigrator.Tests.Unit.Helpers;
using Serilog.Events;
using Serilog.Parsing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: <see cref="MigrationContextEnricher"/> emits <c>DbLogEnabled</c> from the command profile (#6).
/// The property is the gate of <see cref="RayMigratorDatabaseSink"/>; it must follow the command, not the run mode,
/// so that <c>info</c> and <c>validate-hash</c> stay out of the audit log while <c>update-hash</c> is in it.
/// </summary>
public class DbLogEnabledLoggingPipelineTests : IDisposable
{
    public void Dispose()
    {
        MigrationLoggingContext.Current = null;
    }

    [Theory]
    [InlineData(MigrationCommand.Info, MigrationRunMode.Migrate, false, false)]
    [InlineData(MigrationCommand.ValidateHash, MigrationRunMode.Migrate, false, false)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Migrate, true, false)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Simulate, false, false)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Validate, false, false)]
    [InlineData(MigrationCommand.UpdateHash, MigrationRunMode.Migrate, false, true)]
    [InlineData(MigrationCommand.Baseline, MigrationRunMode.Migrate, false, true)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Migrate, false, true)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, false, true)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, false, true)]
    public void Enricher_EmitsDbLogEnabled_FromCommandProfile(MigrationCommand command, MigrationRunMode runMode, bool fixDryRun, bool expected)
    {
        MigrationLoggingContext.Current = ProfileTestContext.CreateContext(command, runMode, "Data Source=:memory:", fixDryRun);
        var logEvent = CreateLogEvent();

        new MigrationContextEnricher().Enrich(logEvent, new TestPropertyFactory());

        logEvent.Properties.Should().ContainKey("DbLogEnabled");
        (logEvent.Properties["DbLogEnabled"] as ScalarValue)!.Value.Should().Be(expected,
            $"'{command}' in '{runMode}' mode{(fixDryRun ? " (dry run)" : "")} {(expected ? "must" : "must not")} leave an audit trail");
    }

    [Fact]
    public void Enricher_StillEmitsRunModeId_ForTheLogRowStamp()
    {
        // RunModeId is no longer the gate but is still stored in every MigrationLog row.
        MigrationLoggingContext.Current = ProfileTestContext.CreateContext(MigrationCommand.UpdateHash, MigrationRunMode.Migrate, "Data Source=:memory:");
        var logEvent = CreateLogEvent();

        new MigrationContextEnricher().Enrich(logEvent, new TestPropertyFactory());

        (logEvent.Properties["RunModeId"] as ScalarValue)!.Value.Should().Be((byte)MigrationRunMode.Migrate);
    }

    [Fact]
    public void Enricher_WithoutContext_EmitsNoDbLogEnabled()
    {
        // Early pipeline logs have no context; the sink treats a missing DbLogEnabled as "pass through".
        MigrationLoggingContext.Current = null;
        var logEvent = CreateLogEvent();

        new MigrationContextEnricher().Enrich(logEvent, new TestPropertyFactory());

        logEvent.Properties.Should().NotContainKey("DbLogEnabled");
    }

    private static LogEvent CreateLogEvent()
        => new(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("Test message"), Enumerable.Empty<LogEventProperty>());

    private class TestPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }
}
