using System.Data.Common;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the initialization of the database log retries <c>DatabaseLogging_CheckCreate</c> after a transient failure as
/// the log writes do, but not after a timeout: the template builds the indexes of the log table on the rows already
/// there, an attempt that timed out was rolled back whole, and a retry would build the index from the start again. The
/// failure then names the index build and the option to raise.
/// </summary>
public class DatabaseLogInitRetryTests
{
    /// <summary>A failure the scripted DAL classifies as transient, such as a deadlock or a lost connection.</summary>
    private sealed class TransientTestException(string message) : Exception(message);

    /// <summary>A DAL whose scalar calls run <see cref="Behaviour"/>, one call per attempt; the other calls are unused.</summary>
    private sealed class ScriptedLogDal : DalBase
    {
        public Func<int, object?> Behaviour { get; set; } = _ => "0,The log exists";

        public int Attempts { get; private set; }

        public override string DatabaseType => "Sqlite";
        public override DalSpecificProperties DalSpecificProperties => new();

        public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
            => ex is TransientTestException ? (true, "T") : base.IsTransient(ex);

        protected override Task<object?> ExecuteScalarCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
            => ExecuteWithRetryAsync(() => Task.FromResult(Behaviour(++Attempts)), dalSettings, cancellationToken: cancellationToken);

        protected override Task ExecuteNonQueryCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task<List<Dictionary<string, object?>>> ExecuteReaderCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task<bool> IsConnectionValidCoreAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task ExecuteNonQueryCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task<object?> ExecuteScalarCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override void CheckConnectionStringOrValidateConnection(bool validateConnection) => throw new NotImplementedException();
        public override DbConnection CreateConnection() => throw new NotImplementedException();
    }

    private static Task<bool> InitAsync(ScriptedLogDal dal)
    {
        var options = new RayMigratorOptions
        {
            DatabaseLogging = new DatabaseLoggingOptions
            {
                Enabled = true, DatabaseType = "Sqlite", ConnectionString = "Data Source=log.sqlite", TableBaseName = "",
                DbCommandTimeoutInSeconds = 20, MinimumLevel = "Information"
            }
        };
        var cache = new TemplateCache(false, NullLogger<TemplateCache>.Instance);
        var writer = new DatabaseLogWriter(options, dal);
        return writer.InitDatabaseLoggerAsync("Sqlite", cache, new TemplateExecutor(cache, NullLogger<TemplateExecutor>.Instance, null!),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InitDatabaseLogger_CheckCreateTimesOut_TriesOnceAndNamesTheIndexBuildAndTheTimeout()
    {
        // Arrange
        var dal = new ScriptedLogDal { Behaviour = _ => throw new TimeoutException("The command timed out.") };

        // Act
        var act = () => InitAsync(dal);

        // Assert
        var thrown = await act.Should().ThrowAsync<TemplateExecutionException>();
        dal.Attempts.Should().Be(1, because: "a retry would build the indexes of the log table from the start again and time out again");
        thrown.Which.Message.Should().Contain("command timeout of the database log (20 s)")
            .And.Contain("indexes of the log table")
            .And.Contain("DatabaseLogging.DbCommandTimeoutInSeconds");
    }

    [Fact]
    public async Task InitDatabaseLogger_TransientFailureThatIsNoTimeout_IsRetried()
    {
        // Arrange: two transient failures, then the template reports an existing log
        var dal = new ScriptedLogDal
        {
            Behaviour = attempt => attempt <= 2 ? throw new TransientTestException("deadlock victim") : "0,The log exists"
        };

        // Act
        bool created = await InitAsync(dal);

        // Assert
        created.Should().BeFalse(because: "the template reported that the log existed");
        dal.Attempts.Should().Be(3, because: "a transient failure that is no timeout is retried as the log writes are");
    }
}
