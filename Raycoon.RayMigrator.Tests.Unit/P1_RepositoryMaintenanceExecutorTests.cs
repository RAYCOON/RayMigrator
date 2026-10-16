using System.Data.Common;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="RepositoryMaintenanceExecutor"/> on a scripted DAL: it runs the retention templates without the retries
/// of the repository (the loop of the maintenance service and the next call of the host are the retry), asks
/// <c>DatabaseLogging_Count</c> for the existence of the log only where it needs no count, and refuses a log count
/// without the code that says whether the log table exists.
/// </summary>
public class RepositoryMaintenanceExecutorTests
{
    private static readonly DateTime Cutoff = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The scalar calls of the executor: their settings and parameters, answered by <see cref="Answer"/>.</summary>
    private sealed class ScriptedDal : DalBase
    {
        public Func<string, object?> Answer { get; set; } = _ => "0,message";

        public List<(string Sql, IDalSettings Settings, DalParameterList? Parameters)> Calls { get; } = [];

        public override string DatabaseType => "Sqlite";
        public override DalSpecificProperties DalSpecificProperties => new();

        protected override Task<object?> ExecuteScalarCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
        {
            Calls.Add((sqlCode, dalSettings, dalParameterList));
            return Task.FromResult(Answer(sqlCode));
        }

        protected override Task ExecuteNonQueryCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task<List<Dictionary<string, object?>>> ExecuteReaderCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task<bool> IsConnectionValidCoreAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task ExecuteNonQueryCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task<object?> ExecuteScalarCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken) => throw new NotImplementedException();
        public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override void CheckConnectionStringOrValidateConnection(bool validateConnection) => throw new NotImplementedException();
        public override DbConnection CreateConnection() => throw new NotImplementedException();
    }

    private static RepositoryOptions Repository() => new()
    {
        DatabaseType = "Sqlite", ConnectionString = "Data Source=repository.sqlite", SchemaName = null, TableBaseName = "",
        DbCommandTimeoutInSeconds = 60, DbCommandMaxRetries = 100, DbCommandWaitTimeInMsBeforeRetry = 250
    };

    private static RepositoryMaintenanceExecutor Executor(ScriptedDal dal) =>
        new(new TemplateCache(false, NullLogger<TemplateCache>.Instance), Repository(), dal);

    private static bool Is(string sql, TemplateType templateType) => sql.Contains($"TemplateType   = \"{templateType}\"", StringComparison.Ordinal);

    [Fact]
    public async Task EveryTemplate_RunsWithoutTheRetriesOfTheRepository()
    {
        // Arrange: the repository allows 100 retries, linear 250 ms, each after a command timeout of 60 seconds
        var dal = new ScriptedDal { Answer = sql => Is(sql, TemplateType.DatabaseLogging_Count) ? "0,1,message" : "0,message" };
        var executor = Executor(dal);

        // Act
        await executor.CountHistoryAsync(Cutoff, TestContext.Current.CancellationToken);
        await executor.CountLogAsync(Cutoff, TestContext.Current.CancellationToken);
        await executor.DeleteHistoryAsync(Cutoff, 10, TestContext.Current.CancellationToken);
        await executor.DeleteLogAsync(Cutoff, 10, TestContext.Current.CancellationToken);

        // Assert
        dal.Calls.Should().HaveCount(4);
        dal.Calls.Should().OnlyContain(c => c.Settings.MaxRetries == 0,
            because: "a retry of a statement that timed out, or of SQLite BUSY after its busy timeout, waits the whole command timeout again");
        dal.Calls.Should().OnlyContain(c => c.Settings.DbCommandTimeoutInSeconds == 60 && !c.Settings.UseTransaction,
            because: "the command timeout of the repository and the autocommit of the templates stay");
    }

    [Theory]
    [InlineData("0,1,MigrationLog exists (not counted)", true)]
    [InlineData("0,0,MigrationLog does not exist", false)]
    public async Task LogTableExists_AsksTheCountForTheExistenceAlone(string answer, bool exists)
    {
        // Arrange
        var dal = new ScriptedDal { Answer = _ => answer };

        // Act
        bool result = await Executor(dal).LogTableExistsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(exists);
        var call = dal.Calls.Should().ContainSingle().Which;
        Is(call.Sql, TemplateType.DatabaseLogging_Count).Should().BeTrue();
        call.Parameters!.GetAllParameters().Should().Contain(p => p.Key == "ExistsOnly" && Equals(p.Value.ParameterValue, 1),
            because: "the cleanup needs no count of a large log before its first batch");
    }

    [Fact]
    public async Task CountLog_CountsTheRows()
    {
        // Arrange
        var dal = new ScriptedDal { Answer = _ => "42,1,[42] MigrationLog rows can be deleted" };

        // Act
        long? rows = await Executor(dal).CountLogAsync(Cutoff, TestContext.Current.CancellationToken);

        // Assert
        rows.Should().Be(42);
        dal.Calls.Should().ContainSingle().Which.Parameters!.GetAllParameters()
            .Should().Contain(p => p.Key == "ExistsOnly" && Equals(p.Value.ParameterValue, 0), because: "the dry run counts the rows");
    }

    [Fact]
    public async Task CountLog_ResultWithoutTheExistenceCode_Throws()
    {
        // Arrange: a count of an external DAL that does not say whether the log table exists
        var dal = new ScriptedDal { Answer = _ => "5,MigrationLog rows can be deleted" };

        // Act
        var act = () => Executor(dal).CountLogAsync(Cutoff, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<TemplateResultException>()).Which.Message.Should().Contain("no second code",
            because: "without it the delete template would run against a log table that may not exist");
    }
}
