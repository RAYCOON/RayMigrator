using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for EnvironmentId propagation through the logging pipeline.
/// Covers:
///   1. MigrationContextEnricher emits EnvironmentId int property (and retains text Environment property).
///   2. DatabaseLogWriter.EnqueueLogEntry null-guards: passes null to DAL when environmentId == 0,
///      passes the actual int value otherwise.
/// Mirrors the style of P2_MigrationRecordIdLoggingPipelineTests.cs.
/// </summary>
public class EnvironmentIdLoggingPipelineTests : IDisposable
{
    public void Dispose()
    {
        MigrationLoggingContext.Current = null;
    }

    #region Helper Methods

    private static MigrationContext CreateTestContext(int environmentId = 0, int productId = 0)
    {
        var rayOptions = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=test",
                SchemaName = "ray",
                TableBaseName = "",
                DbCommandTimeoutInSeconds = 60,
                DbCommandMaxRetries = 0,
                DbCommandWaitTimeInMsBeforeRetry = 250
            },
            ProductDefaults = new ProductDefaultOptions("UTF-8")
            {
                MigrationErrorAction = "Terminate",
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "rollback",
                MigrationFilesEncoding = "UTF-8",
                RequireRollbackFile = false,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Simultaneously",
                    HashValidationScope = "File",
                    TargetDefaults = new TargetDefaultsOptions
                    {
                        DbCommandTimeoutInSeconds = 20,
                        DbCommandMaxRetries = 0,
                        DbCommandWaitTimeInMsBeforeRetry = 250
                    }
                }
            },
            Products = new List<ProductOptions>
            {
                new("rollback")
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    MigrationErrorAction = "Terminate",
                    MigrationFilesExtension = "sql",
                    MigrationRollbackFilesPreExtension = "rollback",
                    MigrationFilesEncoding = "UTF-8",
                    RequireRollbackFile = false,
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new()
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            Targets = new List<TargetOptions>
                            {
                                new()
                                {
                                    Alias = "MainDB",
                                    ConnectionString = "Server=target",
                                    DbCommandTimeoutInSeconds = 20,
                                    DbCommandMaxRetries = 0,
                                    DbCommandWaitTimeInMsBeforeRetry = 250
                                }
                            }
                        }
                    }
                }
            }
        };

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.MigrateUp,
            Product = "TestProduct",
            Environment = "Docker",
            RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = false,
            RevealSensitiveData = false
        };

        var ctx = new MigrationContext(rayOptions, consoleOptions, "3.0.0");
        ctx.MigrationState.EnvironmentId = environmentId;
        ctx.MigrationState.ProductId = productId;
        return ctx;
    }

    private static LogEvent CreateEmptyLogEvent()
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("Test"),
            Enumerable.Empty<LogEventProperty>());
    }

    private static DatabaseLogWriter CreateInitializedWriter(IDal dal)
    {
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

        var initializedField = typeof(DatabaseLogWriter).GetField(
            "_isDatabaseLoggingInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        initializedField!.SetValue(writer, true);

        var templateField = typeof(DatabaseLogWriter).GetField(
            "_templateLoggingInsert",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        templateField!.SetValue(writer, new Template { Content = "INSERT INTO logs" });

        return writer;
    }

    private static bool WaitForCondition(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
            Thread.Sleep(50);
        return condition();
    }

    private class TestPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }

    #endregion

    #region MigrationContextEnricher — EnvironmentId scalar emission

    [Fact]
    public void Enricher_EmitsEnvironmentId_WhenContextHasNonZeroEnvironmentId()
    {
        // Arrange
        var ctx = CreateTestContext(environmentId: 42);
        MigrationLoggingContext.Current = ctx;

        var enricher = new MigrationContextEnricher();
        var logEvent = CreateEmptyLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("EnvironmentId",
            "MigrationContextEnricher must emit an EnvironmentId property");
        var scalar = logEvent.Properties["EnvironmentId"] as ScalarValue;
        scalar.Should().NotBeNull();
        scalar!.Value.Should().Be(42,
            "EnvironmentId property must carry the int value from MigrationState.EnvironmentId");
    }

    [Fact]
    public void Enricher_EmitsEnvironmentIdZero_WhenContextHasDefaultEnvironmentId()
    {
        // Arrange — EnvironmentId = 0 (default, before Repository_Environment_CheckInsert runs)
        var ctx = CreateTestContext(environmentId: 0);
        MigrationLoggingContext.Current = ctx;

        var enricher = new MigrationContextEnricher();
        var logEvent = CreateEmptyLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("EnvironmentId");
        var scalar = logEvent.Properties["EnvironmentId"] as ScalarValue;
        scalar!.Value.Should().Be(0,
            "EnvironmentId = 0 (not yet resolved) must still be emitted as 0");
    }

    [Fact]
    public void Enricher_AlsoEmitsTextEnvironmentProperty_ForConsoleFileOutput()
    {
        // The text Environment property must survive alongside EnvironmentId
        // so that human-readable console/file log output still shows the environment name.
        var ctx = CreateTestContext(environmentId: 5);
        MigrationLoggingContext.Current = ctx;

        var enricher = new MigrationContextEnricher();
        var logEvent = CreateEmptyLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("Environment",
            "the text 'Environment' property must still be emitted for human-readable log output");
        var envScalar = logEvent.Properties["Environment"] as ScalarValue;
        envScalar!.Value.Should().Be("Docker",
            "the text Environment property must carry the console option value");
    }

    [Fact]
    public void Enricher_DoesNotEmitAnyProperty_WhenContextIsNull()
    {
        MigrationLoggingContext.Current = null;

        var enricher = new MigrationContextEnricher();
        var logEvent = CreateEmptyLogEvent();

        enricher.Enrich(logEvent, new TestPropertyFactory());

        logEvent.Properties.Should().NotContainKey("EnvironmentId",
            "no properties must be emitted when MigrationLoggingContext is null");
        logEvent.Properties.Should().NotContainKey("Environment");
    }

    #endregion

    #region DatabaseLogWriter — EnvironmentId null-guard (0 → null in DAL parameter)

    [Fact]
    public void Writer_PassesActualEnvironmentId_WhenValueIsPositive()
    {
        // Arrange
        var dal = Substitute.For<IDal>();
        DalParameterList? capturedParams = null;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(callInfo => capturedParams = callInfo.ArgAt<DalParameterList>(2));

        var writer = CreateInitializedWriter(dal);

        // Act
        writer.EnqueueLogEntry(
            LogLevel.Information, 0, "Test",
            runModeId: 100, productId: 3, environmentId: 7,
            migrationRunId: 1, migrationRecordId: 0,
            releaseVersion: "1.0", targetGroupAlias: "Backend", targetAlias: "MainDB",
            fileName: null, fileOrderId: 0, fileBlockId: 0);

        WaitForCondition(() => capturedParams != null);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.TryGetValue("EnvironmentId", out var envParam).Should().BeTrue(
            "DAL parameter list must contain 'EnvironmentId'");
        envParam!.ParameterValue.Should().Be(7,
            "EnvironmentId = 7 (positive) must be forwarded as-is to the DAL");
        envParam.ParameterType.Should().Be(typeof(int?));
    }

    [Fact]
    public void Writer_PassesNullEnvironmentId_WhenValueIsZero()
    {
        // Arrange — environmentId = 0 means "not yet resolved" → must become null in DAL
        var dal = Substitute.For<IDal>();
        DalParameterList? capturedParams = null;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(callInfo => capturedParams = callInfo.ArgAt<DalParameterList>(2));

        var writer = CreateInitializedWriter(dal);

        // Act
        writer.EnqueueLogEntry(
            LogLevel.Information, 0, "Test",
            runModeId: 100, productId: 3, environmentId: 0,
            migrationRunId: 1, migrationRecordId: 0,
            releaseVersion: "1.0", targetGroupAlias: "Backend", targetAlias: "MainDB",
            fileName: null, fileOrderId: 0, fileBlockId: 0);

        WaitForCondition(() => capturedParams != null);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.TryGetValue("EnvironmentId", out var envParam).Should().BeTrue();
        envParam!.ParameterValue.Should().BeNull(
            "EnvironmentId = 0 must be converted to null in the DAL parameter (not-yet-resolved sentinel)");
        envParam.ParameterType.Should().Be(typeof(int?));
    }

    [Fact]
    public void Writer_PassesNullEnvironmentId_WhenValueIsNull()
    {
        // Arrange — environmentId = null (early logs before context is known)
        var dal = Substitute.For<IDal>();
        DalParameterList? capturedParams = null;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(callInfo => capturedParams = callInfo.ArgAt<DalParameterList>(2));

        var writer = CreateInitializedWriter(dal);

        // Act
        writer.EnqueueLogEntry(
            LogLevel.Information, 0, "Test",
            runModeId: 100, productId: 3, environmentId: null,
            migrationRunId: 1, migrationRecordId: 0,
            releaseVersion: "1.0", targetGroupAlias: "Backend", targetAlias: "MainDB",
            fileName: null, fileOrderId: 0, fileBlockId: 0);

        WaitForCondition(() => capturedParams != null);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.TryGetValue("EnvironmentId", out var envParam).Should().BeTrue();
        envParam!.ParameterValue.Should().BeNull(
            "null environmentId must pass through as null to the DAL parameter");
        envParam.ParameterType.Should().Be(typeof(int?));
    }

    [Fact]
    public void Writer_DoesNotCallDal_WhenNotInitialized()
    {
        // Sanity guard: EnqueueLogEntry must short-circuit when not initialized
        var dal = Substitute.For<IDal>();
        bool dalCalled = false;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(_ => dalCalled = true);

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

        // Not initialized (no SetValue hack)
        var writer = new DatabaseLogWriter(options, dal);

        writer.EnqueueLogEntry(
            LogLevel.Information, 0, "Test",
            runModeId: 100, productId: 3, environmentId: 7,
            migrationRunId: 1, migrationRecordId: 0,
            releaseVersion: null, targetGroupAlias: null, targetAlias: null,
            fileName: null, fileOrderId: 0, fileBlockId: 0);

        Thread.Sleep(150);

        dalCalled.Should().BeFalse(
            "DAL must not be called when DatabaseLogWriter is not initialized");
    }

    #endregion
}
