using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="RepositoryMaintenanceService"/> deletes old history and log rows from the repository options alone (no
/// product, no context), on a SQLite file: it validates the options like the repository schema service before the
/// database is touched, refuses an incomplete request, a database without a repository and a repository newer than the
/// engine, cleans an older repository without upgrading it, and skips a log that is not in the repository database.
/// The deletion itself is covered on every engine by the engine tests (<c>RepositoryMaintenanceContract</c>).
/// </summary>
public class RepositoryMaintenanceServiceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"raymigrator-maintenance-service-{Guid.NewGuid():N}.sqlite");

    private string ConnectionString => $"Data Source={_file}";

    private static DateTime Tomorrow => DateTime.UtcNow.AddDays(1);

    private RepositoryOptions Repository() => new() { DatabaseType = "Sqlite", ConnectionString = ConnectionString };

    private RepositoryQueryHelper Query() => new("Sqlite", ConnectionString, "");

    private static TemplateCache Cache() => new(false, NullLogger<TemplateCache>.Instance);

    private static RepositoryMaintenanceService CreateService() => new(Cache(), NullLogger<RepositoryMaintenanceService>.Instance);

    private async Task CreateRepositoryAsync() =>
        await new RepositorySchemaService(Cache(), NullLogger<RepositorySchemaService>.Instance).EnsureAsync(Repository(), TestContext.Current.CancellationToken);

    public void Dispose()
    {
        if (File.Exists(_file))
            File.Delete(_file);
    }

    #region Request

    [Fact]
    public async Task CleanUp_WithoutACutoff_IsRefusedBeforeTheDatabaseIsTouched()
    {
        // Act
        var act = () => CreateService().CleanUpAsync(Repository(), new RetentionRequest(null, null));

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("HistoryCutoffUtc").And.Contain("LogCutoffUtc");
        File.Exists(_file).Should().BeFalse(because: "a request without a cutoff never opens the database");
    }

    [Theory]
    [InlineData(DateTimeKind.Local, true)]
    [InlineData(DateTimeKind.Unspecified, true)]
    [InlineData(DateTimeKind.Local, false)]
    [InlineData(DateTimeKind.Unspecified, false)]
    public async Task CleanUp_CutoffThatIsNotUtc_IsRefused(DateTimeKind kind, bool history)
    {
        // Arrange
        var cutoff = DateTime.SpecifyKind(new DateTime(2026, 10, 1, 12, 0, 0), kind);
        var request = history ? new RetentionRequest(cutoff, null) : new RetentionRequest(null, cutoff);

        // Act
        var act = () => CreateService().CleanUpAsync(Repository(), request);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should()
            .Contain(history ? "HistoryCutoffUtc" : "LogCutoffUtc").And.Contain("DateTimeKind.Utc").And.Contain($"[{kind}]");
        File.Exists(_file).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CleanUp_BatchSizeBelowOne_IsRefused(int batchSize)
    {
        // Act
        var act = () => CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, null, BatchSize: batchSize));

        // Assert
        (await act.Should().ThrowAsync<ArgumentOutOfRangeException>()).Which.Message.Should().Contain("BatchSize");
        File.Exists(_file).Should().BeFalse();
    }

    [Fact]
    public async Task CleanUp_NegativeTimeBudget_IsRefused()
    {
        // Act
        var act = () => CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, null, TimeBudget: TimeSpan.FromSeconds(-1)));

        // Assert
        (await act.Should().ThrowAsync<ArgumentOutOfRangeException>()).Which.Message.Should().Contain("TimeBudget").And.Contain("InfiniteTimeSpan");
        File.Exists(_file).Should().BeFalse();
    }

    [Fact]
    public async Task CleanUp_InfiniteTimeBudget_CleansWithoutALimit()
    {
        // Arrange
        await CreateRepositoryAsync();

        // Act
        var result = await CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, null, TimeBudget: Timeout.InfiniteTimeSpan),
            TestContext.Current.CancellationToken);

        // Assert
        result.Completed.Should().BeTrue(because: "Timeout.InfiniteTimeSpan sets no limit");
        result.HistoryRows.Should().Be(0);
        result.Batches.Should().Be(0, because: "the one batch found nothing to delete");
    }

    [Fact]
    public void RetentionRequest_WithoutOptionalValues_UsesTheDocumentedDefaults()
    {
        // Act
        var request = new RetentionRequest(Tomorrow, null);

        // Assert
        request.BatchSize.Should().Be(RetentionRequest.DefaultBatchSize);
        RetentionRequest.DefaultBatchSize.Should().BeLessThan(5000, because: "SQL Server escalates a statement with 5,000 locks on one index to a table lock");
        request.TimeBudget.Should().BeNull(because: "null means the default budget");
        RetentionRequest.DefaultTimeBudget.Should().Be(TimeSpan.FromSeconds(60));
        request.DryRun.Should().BeFalse();
    }

    #endregion Request

    #region Repository options

    [Fact]
    public async Task CleanUp_WithoutDatabaseType_IsRefusedBeforeTheDatabaseIsTouched()
    {
        // Act
        var act = () => CreateService().CleanUpAsync(new RepositoryOptions { ConnectionString = ConnectionString }, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        (await act.Should().ThrowAsync<ConfigurationValidationException>()).Which.Message.Should().Contain("DatabaseType");
        File.Exists(_file).Should().BeFalse();
    }

    [Fact]
    public async Task CleanUp_WithoutConnectionString_IsRefused()
    {
        // Act
        var act = () => CreateService().CleanUpAsync(new RepositoryOptions { DatabaseType = "Sqlite" }, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        (await act.Should().ThrowAsync<ConfigurationValidationException>()).Which.Message.Should().Contain("ConnectionString");
    }

    [Fact]
    public async Task CleanUp_UnknownDatabaseType_IsRefusedWithTheAvailableTypes()
    {
        // Act
        var act = () => CreateService().CleanUpAsync(new RepositoryOptions { DatabaseType = "Oracle", ConnectionString = ConnectionString }, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        (await act.Should().ThrowAsync<ConfigurationValidationException>()).Which.Message.Should().Contain("[Oracle]").And.Contain("Sqlite");
        File.Exists(_file).Should().BeFalse();
    }

    [Fact]
    public async Task CleanUp_UnknownDatabaseTypeInAnotherCase_NamesTheExactName()
    {
        // Act
        var act = () => CreateService().CleanUpAsync(new RepositoryOptions { DatabaseType = "sqlite", ConnectionString = ConnectionString }, new RetentionRequest(Tomorrow, null));

        // Assert
        (await act.Should().ThrowAsync<ConfigurationValidationException>()).Which.Message.Should().Contain("Did you mean [Sqlite]");
    }

    [Fact]
    public async Task CleanUp_RefusesAPrefixTheRunsWouldRefuse()
    {
        // Arrange: longer than RULE_4_4 allows
        var tooLong = new RepositoryOptions { DatabaseType = "Sqlite", ConnectionString = ConnectionString, TableBaseName = "seventeen_chars__" };

        // Act
        var act = () => CreateService().CleanUpAsync(tooLong, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        (await act.Should().ThrowAsync<ConfigurationValidationException>()).Which.Message.Should().Contain("RULE_4_4").And.Contain("TableBaseName");
        File.Exists(_file).Should().BeFalse(because: "nothing is touched for options a run would refuse");
    }

    [Fact]
    public async Task CleanUp_RefusesAnUppercasePrefix_AndAMissingSchemaName_BeforeTheDatabaseIsTouched()
    {
        // Arrange: the connection strings point nowhere; the rules refuse the options first.
        var service = CreateService();
        var request = new RetentionRequest(Tomorrow, Tomorrow);

        // Act
        var uppercase = () => service.CleanUpAsync(new RepositoryOptions { DatabaseType = "PostgreSQL", ConnectionString = "Host=nowhere.invalid;Database=x", SchemaName = "ray", TableBaseName = "Rm_" }, request);
        var noSchema = () => service.CleanUpAsync(new RepositoryOptions { DatabaseType = "SqlServer", ConnectionString = "Server=nowhere.invalid;Database=x" }, request);

        // Assert
        await uppercase.Should().ThrowAsync<ConfigurationValidationException>().WithMessage("*RULE_4_3*");
        await noSchema.Should().ThrowAsync<ConfigurationValidationException>().WithMessage("*RULE_4_2*");
    }

    [Fact]
    public async Task CleanUp_ResolvesPlaceholders_AndRefusesAMissingVariable()
    {
        // Arrange
        await CreateRepositoryAsync();
        var service = CreateService();
        string variable = $"RM_MAINTENANCE_SERVICE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, ConnectionString);
        try
        {
            // Act
            var result = await service.CleanUpAsync(new RepositoryOptions { DatabaseType = "Sqlite", ConnectionString = $"{{ENV:{variable}}}" },
                new RetentionRequest(Tomorrow, null), TestContext.Current.CancellationToken);

            // Assert
            result.Completed.Should().BeTrue(because: "the placeholder named the SQLite file of the repository");
            File.Exists(Path.Combine(Environment.CurrentDirectory, $"{{ENV:{variable}}}")).Should().BeFalse(because: "no file is named after the placeholder");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        var missing = () => service.CleanUpAsync(new RepositoryOptions { DatabaseType = "Sqlite", ConnectionString = "{ENV:RM_MAINTENANCE_SERVICE_UNSET}" }, new RetentionRequest(Tomorrow, null));
        await missing.Should().ThrowAsync<ConfigurationValidationException>().WithMessage("*RM_MAINTENANCE_SERVICE_UNSET*");
    }

    [Fact]
    public async Task CleanUp_WritesTheDocumentedDefaultsIntoTheOptions()
    {
        // Arrange
        await CreateRepositoryAsync();
        var repository = Repository();

        // Act
        await CreateService().CleanUpAsync(repository, new RetentionRequest(Tomorrow, null), TestContext.Current.CancellationToken);

        // Assert
        repository.DbCommandTimeoutInSeconds.Should().Be(60);
        repository.DbCommandMaxRetries.Should().Be(100);
        repository.DbCommandWaitTimeInMsBeforeRetry.Should().Be(250);
        repository.TableBaseName.Should().Be(string.Empty);
    }

    #endregion Repository options

    #region Schema version

    [Fact]
    public async Task CleanUp_DatabaseWithoutRepository_IsRefused()
    {
        // Act
        var act = () => CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        var ex = (await act.Should().ThrowAsync<RepositorySchemaException>()).Which;
        ex.RepositoryVersion.Should().Be(0);
        ex.SupportedVersion.Should().Be(RepositorySchema.CurrentVersion);
        ex.Message.Should().Contain("has no RayMigrator repository");
        Query().TableExists("MigrationRecordHistory").Should().BeFalse(because: "the check creates no repository table");
    }

    [Fact]
    public async Task CleanUp_RepositoryNewerThanTheEngine_IsRefused()
    {
        // Arrange
        await CreateRepositoryAsync();
        Query().ExecuteOnConnection(ConnectionString,
            "INSERT INTO \"RepositorySchemaVersion\" (\"Version\", \"AppliedAt\", \"RayMigratorVersion\") VALUES (99, datetime('now'), 'future')");

        // Act
        var act = () => CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        var ex = (await act.Should().ThrowAsync<RepositorySchemaException>()).Which;
        ex.RepositoryVersion.Should().Be(99);
        ex.SupportedVersion.Should().Be(RepositorySchema.CurrentVersion);
    }

    [Fact]
    public async Task CleanUp_RepositoryOfAnOlderSchemaVersion_IsCleanedWithoutAnUpgrade()
    {
        // Arrange: a repository at schema version 1, created through the executor with that target.
        var versionOne = new RepositoryOptions { DatabaseType = "Sqlite", ConnectionString = ConnectionString, TableBaseName = "", DbCommandTimeoutInSeconds = 60, DbCommandMaxRetries = 100, DbCommandWaitTimeInMsBeforeRetry = 250 };
        DalFactory.TryGetDal("Sqlite", ConnectionString, out var dal).Should().BeTrue();
        await new RepositorySchemaExecutor(Cache(), NullLogger.Instance, versionOne, dal!, "test", 1).EnsureAsync(TestContext.Current.CancellationToken);
        var logger = new Helpers.CapturingLogger<RepositoryMaintenanceService>();

        // Act
        var result = await new RepositoryMaintenanceService(Cache(), logger).CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.Completed.Should().BeTrue(because: "the templates read only columns of schema version 1");
        Query().CountRows("RepositorySchemaVersion").Should().Be(1, because: "a cleanup never upgrades the repository");
        logger.Entries.Should().ContainSingle(e => e.LogLevel == LogLevel.Warning)
            .Which.Message.Should().Contain("schema version [1]").And.Contain("does not upgrade");
    }

    #endregion Schema version

    #region Log, dry run and cancellation

    [Fact]
    public async Task CleanUp_RepositoryWithoutLogTable_SkipsTheLogAndLeavesNoTableBehind()
    {
        // Arrange: a repository whose log is written elsewhere or not at all
        await CreateRepositoryAsync();

        // Act
        var result = await CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, Tomorrow), TestContext.Current.CancellationToken);

        // Assert
        result.LogSkipped.Should().BeTrue();
        result.LogRows.Should().Be(0);
        result.Completed.Should().BeTrue(because: "a log that is not in the repository database leaves nothing to delete there");
        result.Message.Should().Contain("no log table");
        Query().TableExists("MigrationLog").Should().BeFalse(because: "the empty table the SQLite count prepares against is rolled back");
    }

    [Fact]
    public async Task CleanUp_DryRunWithoutLogTable_CountsAndSkipsTheLog()
    {
        // Arrange
        await CreateRepositoryAsync();

        // Act
        var result = await CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, Tomorrow, DryRun: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(new RetentionResult(0, 0, 0, true, true, result.Message));
        result.Message.Should().StartWith("Dry run").And.Contain("no log table");
        Query().TableExists("MigrationLog").Should().BeFalse();
    }

    [Fact]
    public async Task CleanUp_WriteLockHeldBeyondTheCommandTimeout_FailsAfterOneWaitWithoutRetries()
    {
        // Arrange: options that allow eight retries, a command timeout of one second, another connection holds the write lock
        await CreateRepositoryAsync();
        await using var blocker = new SqliteConnection(ConnectionString + ";Pooling=False");
        await blocker.OpenAsync(TestContext.Current.CancellationToken);
        await using (var begin = blocker.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            await begin.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var repository = new RepositoryOptions
        {
            DatabaseType = "Sqlite", ConnectionString = ConnectionString, DbCommandTimeoutInSeconds = 1, DbCommandMaxRetries = 8, DbCommandWaitTimeInMsBeforeRetry = 100
        };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var act = () => CreateService().CleanUpAsync(repository, new RetentionRequest(Tomorrow, null, TimeBudget: TimeSpan.FromSeconds(1)),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<TemplateExecutionException>(because: "the first batch failed before it deleted a row");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4),
            because: "the cleanup waits once for the lock, the busy timeout of SQLite, and does not retry it eight times");
    }

    [Fact]
    public async Task CleanUp_TokenCancelledBeforeTheCall_ThrowsOperationCanceledExceptionWithoutTouchingTheDatabase()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => CreateService().CleanUpAsync(Repository(), new RetentionRequest(Tomorrow, Tomorrow), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>(because: "a cancellation before the cleanup deletes ends the call");
        File.Exists(_file).Should().BeFalse(because: "the cancelled call never opened the SQLite file");
    }

    #endregion Log, dry run and cancellation

    [Fact]
    public void AddRayMigratorServices_RegistersTheMaintenanceService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Cache());
        services.AddRayMigratorServices(RayMigratorHostMode.Api);

        // Act
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IRepositoryMaintenanceService>();

        // Assert
        service.Should().BeOfType<RepositoryMaintenanceService>();
        provider.GetRequiredService<IRepositoryMaintenanceService>().Should().BeSameAs(service, because: "the service is a singleton");
    }
}
