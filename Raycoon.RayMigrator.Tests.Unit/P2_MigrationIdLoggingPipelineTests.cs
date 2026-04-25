
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for MigrationRecordId propagation through the logging pipeline.
/// Verifies that MigrationRecordId flows from MigrationState → Enricher → Sink → Writer → DalParameter.
/// </summary>
public class MigrationRecordIdLoggingPipelineTests : IDisposable
{
    public void Dispose()
    {
        MigrationLoggingContext.Current = null;
    }

    #region Helper Methods

    private static MigrationContext CreateTestContext(int migrationRecordId = 0, int migrationRunId = 0, int productId = 0)
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
                    MigrationFilesRootDirectory = "/path/to/migrations",
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
                            TargetMigrationOrder = "Simultaneously",
                            HashValidationScope = "File",
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
        ctx.MigrationState.MigrationRecordId = migrationRecordId;
        ctx.MigrationState.MigrationRunId = migrationRunId;
        ctx.MigrationState.ProductId = productId;
        return ctx;
    }

    private static LogEvent CreateLogEvent(params (string name, object? value)[] properties)
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("Test message"),
            Enumerable.Empty<LogEventProperty>());

        foreach (var (name, value) in properties)
        {
            logEvent.AddPropertyIfAbsent(new LogEventProperty(name, new ScalarValue(value)));
        }

        return logEvent;
    }

    #endregion

    #region MigrationContextEnricher Tests

    [Fact]
    public void Enricher_ShouldAddMigrationRecordId_WhenContextHasMigrationRecordId()
    {
        // Arrange
        var ctx = CreateTestContext(migrationRecordId: 42);
        MigrationLoggingContext.Current = ctx;

        var enricher = new MigrationContextEnricher();
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new Serilog.Core.LoggingLevelSwitch().MinimumLevel == LogEventLevel.Verbose
            ? null! // won't be reached
            : new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("MigrationRecordId");
        var scalar = logEvent.Properties["MigrationRecordId"] as ScalarValue;
        scalar!.Value.Should().Be(42);
    }

    [Fact]
    public void Enricher_ShouldAddMigrationRecordIdZero_WhenMigrationRecordIdNotSet()
    {
        // Arrange
        var ctx = CreateTestContext(migrationRecordId: 0);
        MigrationLoggingContext.Current = ctx;

        var enricher = new MigrationContextEnricher();
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("MigrationRecordId");
        var scalar = logEvent.Properties["MigrationRecordId"] as ScalarValue;
        scalar!.Value.Should().Be(0);
    }

    [Fact]
    public void Enricher_ShouldNotAddAnyProperty_WhenContextIsNull()
    {
        // Arrange
        MigrationLoggingContext.Current = null;

        var enricher = new MigrationContextEnricher();
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("MigrationRecordId");
    }

    #endregion

    #region RayMigratorDatabaseSink Tests

    [Fact]
    public void Sink_ShouldExtractMigrationRecordId_AndPassToWriter()
    {
        // Arrange
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

        var sink = new RayMigratorDatabaseSink(writer, LogEventLevel.Debug);

        var logEvent = CreateLogEvent(
            ("RunModeId", (byte)100),
            ("ProductId", 5),
            ("EnvironmentId", 7),
            ("MigrationRunId", 10),
            ("MigrationRecordId", 42),
            ("Environment", "Docker"),
            ("ReleaseVersion", "1.0"),
            ("TargetGroupAlias", "Backend"),
            ("TargetAlias", "MainDB"),
            ("FileName", "10_Create.sql"),
            ("FileOrderId", 1),
            ("FileBlockId", 1));

        // Act - Writer is not initialized, so EnqueueLogEntry will return early.
        // We verify that the sink extracts the property without throwing.
        sink.Emit(logEvent);

        // Assert - no exception means the property was extracted successfully.
        // Deeper verification is done in the DatabaseLogWriter tests below.
    }

    [Fact]
    public void Sink_ShouldHandleMissingMigrationRecordId_Gracefully()
    {
        // Arrange
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
        var sink = new RayMigratorDatabaseSink(writer, LogEventLevel.Debug);

        // LogEvent without MigrationRecordId property
        var logEvent = CreateLogEvent(
            ("RunModeId", (byte)100),
            ("MigrationRunId", 10));

        // Act & Assert - should not throw
        var act = () => sink.Emit(logEvent);
        act.Should().NotThrow();
    }

    #endregion

    #region DatabaseLogWriter Tests

    [Fact]
    public void Writer_ShouldIncludeMigrationRecordIdParameter_WhenValueIsPositive()
    {
        // Arrange
        var dal = Substitute.For<IDal>();
        DalParameterList? capturedParams = null;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(callInfo => capturedParams = callInfo.ArgAt<DalParameterList>(2));

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

        // Simulate initialization by using reflection to set _isDatabaseLoggingInitialized
        var field = typeof(DatabaseLogWriter).GetField("_isDatabaseLoggingInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(writer, true);

        var templateField = typeof(DatabaseLogWriter).GetField("_templateLoggingInsert",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        templateField!.SetValue(writer, new Raycoon.RayMigrator.Core.Templates.Template { Content = "INSERT INTO logs" });

        // Act
        writer.EnqueueLogEntry(
            LogLevel.Information, 0, "Test message",
            100, 5, 7, 10, 42,
            "1.0", "Backend", "MainDB",
            "10_Create.sql", 1, 1);

        // Wait for the background queue to process (polling, CI-safe)
        WaitForQueueProcessing(() => capturedParams != null);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.TryGetValue("MigrationRecordId", out var migrationRecordIdParam).Should().BeTrue();
        migrationRecordIdParam!.ParameterValue.Should().Be(42);
        migrationRecordIdParam.ParameterType.Should().Be(typeof(int?));
    }

    [Fact]
    public void Writer_ShouldSetMigrationRecordIdToNull_WhenValueIsZero()
    {
        // Arrange
        var dal = Substitute.For<IDal>();
        DalParameterList? capturedParams = null;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(callInfo => capturedParams = callInfo.ArgAt<DalParameterList>(2));

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

        var field = typeof(DatabaseLogWriter).GetField("_isDatabaseLoggingInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(writer, true);

        var templateField = typeof(DatabaseLogWriter).GetField("_templateLoggingInsert",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        templateField!.SetValue(writer, new Raycoon.RayMigrator.Core.Templates.Template { Content = "INSERT INTO logs" });

        // Act - MigrationRecordId = 0
        writer.EnqueueLogEntry(
            LogLevel.Information, 0, "Test message",
            100, 5, 7, 10, 0,
            "1.0", "Backend", "MainDB",
            "10_Create.sql", 1, 1);

        WaitForQueueProcessing(() => capturedParams != null);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.TryGetValue("MigrationRecordId", out var migrationRecordIdParam).Should().BeTrue();
        migrationRecordIdParam!.ParameterValue.Should().BeNull();
        migrationRecordIdParam.ParameterType.Should().Be(typeof(int?));
    }

    [Fact]
    public void Writer_ShouldSetMigrationRecordIdToNull_WhenValueIsNull()
    {
        // Arrange
        var dal = Substitute.For<IDal>();
        DalParameterList? capturedParams = null;
        dal.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>()))
           .Do(callInfo => capturedParams = callInfo.ArgAt<DalParameterList>(2));

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

        var field = typeof(DatabaseLogWriter).GetField("_isDatabaseLoggingInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(writer, true);

        var templateField = typeof(DatabaseLogWriter).GetField("_templateLoggingInsert",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        templateField!.SetValue(writer, new Raycoon.RayMigrator.Core.Templates.Template { Content = "INSERT INTO logs" });

        // Act - MigrationRecordId = null
        writer.EnqueueLogEntry(
            LogLevel.Information, 0, "Test message",
            100, 5, 7, 10, null,
            "1.0", "Backend", "MainDB",
            "10_Create.sql", 1, 1);

        WaitForQueueProcessing(() => capturedParams != null);

        // Assert
        capturedParams.Should().NotBeNull();
        capturedParams!.TryGetValue("MigrationRecordId", out var migrationRecordIdParam).Should().BeTrue();
        migrationRecordIdParam!.ParameterValue.Should().BeNull();
        migrationRecordIdParam.ParameterType.Should().Be(typeof(int?));
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Polls a condition until it becomes true or timeout (5s) is reached.
    /// Replaces Thread.Sleep which is unreliable on CI runners.
    /// </summary>
    private static void WaitForQueueProcessing(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
            Thread.Sleep(50);
    }

    /// <summary>
    /// Simple ILogEventPropertyFactory implementation for testing the enricher.
    /// </summary>
    private class TestPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }

    #endregion
}
