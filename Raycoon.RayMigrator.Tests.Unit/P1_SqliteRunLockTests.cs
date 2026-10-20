using System.Globalization;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.Sqlite;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The run lock on SQLite holds for starts at the same moment. <c>Repository_MigrationRun_Insert</c> checks for an
/// open run of the product and environment and inserts the run and its MigrationRunMeta row in one
/// <c>BEGIN IMMEDIATE</c> transaction, so a parallel start waits for the write lock and then finds the open run. Eight
/// DALs of their own run the template of the engine at once against one file, in both journal modes a repository file
/// can have (WAL, which the validation of a log connection sets, and the rollback journal of a file without it), in
/// ten rounds, because the race of two starts is narrow. The engine contract <c>RunLockContract</c> checks the same on
/// every engine.
/// </summary>
public class SqliteRunLockTests : IDisposable
{
    /// <summary>How many starts run the template at the same moment in each round.</summary>
    private const int ParallelStarts = 8;

    /// <summary>How many rounds of parallel starts run.</summary>
    private const int Rounds = 10;

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"raymigrator-run-lock-{Guid.NewGuid():N}.sqlite");

    private string ConnectionString => $"Data Source={_file}";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string path in new[] { _file, _file + "-wal", _file + "-shm", _file + "-journal" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Theory]
    [InlineData("WAL")]
    [InlineData("DELETE")]
    public async Task ParallelStarts_TakeTheRunLockOnce(string journalMode)
    {
        // Arrange: a repository with a product, an environment and the engine version, in the journal mode of the case
        await ExecuteAsync($"PRAGMA journal_mode={journalMode};");
        var repository = new RepositoryOptions { DatabaseType = "Sqlite", ConnectionString = ConnectionString };
        var cache = new TemplateCache(false, NullLogger<TemplateCache>.Instance);
        var schema = await new RepositorySchemaService(cache, NullLogger<RepositorySchemaService>.Instance)
            .EnsureAsync(repository, TestContext.Current.CancellationToken);
        var settings = repository.GetDalSettings();
        int productId = await RegisterAsync(cache, repository, settings, TemplateType.Repository_Product_CheckInsert, "LockProduct");
        int environmentId = await RegisterAsync(cache, repository, settings, TemplateType.Repository_Environment_CheckInsert, "LockEnvironment");
        var insert = cache.GetRepositoryTemplate(TemplateType.Repository_MigrationRun_Insert, repository);
        var findings = new List<string>();
        int surplusRuns = 0;

        // Act
        for (int round = 1; round <= Rounds; round++)
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = Enumerable.Range(0, ParallelStarts)
                .Select(_ => InsertRunAsync(new DalSqlite(ConnectionString), insert, settings, productId, environmentId, schema.MigratorMetaId, start.Task))
                .ToList();
            start.SetResult();
            StartResult[] results = await Task.WhenAll(starts);

            int taken = results.Count(r => r.Failure == null && r.Code > 0);
            int refused = results.Count(r => r.Failure == null && r.Code == TemplateResultCode.MigrationAlreadyRunning);
            long open = await CountAsync("SELECT COUNT(*) FROM \"MigrationRun\" WHERE \"FinishedAt\" IS NULL;");
            long withoutMeta = await CountAsync(
                "SELECT COUNT(*) FROM \"MigrationRun\" AS r WHERE NOT EXISTS (SELECT 1 FROM \"MigrationRunMeta\" AS m WHERE m.\"MigrationRunId\" = r.\"Id\");");
            if (taken != 1 || refused != ParallelStarts - 1 || open != 1 || withoutMeta != 0)
                findings.Add($"round {round}: [{string.Join(", ", results)}], {open} open run(s), {withoutMeta} run(s) without MigrationRunMeta");

            surplusRuns += Math.Max(0, taken - 1);

            // Every run a start inserted is closed, so that the next round starts without an open run
            await ExecuteAsync("UPDATE \"MigrationRun\" SET \"MigrationRunResultId\" = 100, \"FinishedAt\" = datetime('now') WHERE \"FinishedAt\" IS NULL;");
        }

        // Assert
        findings.Should().BeEmpty(
            because: "in every round exactly one of {0} starts takes the run lock with its run and its MigrationRunMeta row, and the other starts are refused with -2, but {1} of {2} rounds broke it with {3} runs too many ({4})",
            ParallelStarts, findings.Count, Rounds, surplusRuns, string.Join("; ", findings));
    }

    /// <summary>The result of one start: the code the template returned, or the failure of a start that threw.</summary>
    private readonly record struct StartResult(int Code, string? Failure)
    {
        public override string ToString() => Failure ?? Code.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Registers a product or an environment through its CheckInsert template and returns its Id.</summary>
    private async Task<int> RegisterAsync(TemplateCache cache, RepositoryOptions repository, DalSettings settings, TemplateType templateType, string name)
    {
        var parameters = new DalParameterList();
        parameters.AddParameter(new DalParameter("Name", name, typeof(string)));
        parameters.AddParameter(new DalParameter("NameLower", name.ToLowerInvariant(), typeof(string)));
        var response = await TemplateExecutor.ExecuteScalarAsync(
            cache.GetRepositoryTemplate(templateType, repository), new DalSqlite(ConnectionString), settings, parameters,
            TestContext.Current.CancellationToken);
        return response.ResultCode;
    }

    /// <summary>
    /// Runs <c>Repository_MigrationRun_Insert</c> on <paramref name="dal"/> with the parameters of a migrate-up once
    /// <paramref name="start"/> completes: the Id of the run it inserted, <c>-2</c> when it found an open run, or the
    /// exception of a start that failed.
    /// </summary>
    private static async Task<StartResult> InsertRunAsync(IDal dal, Template insert, DalSettings settings, int productId, int environmentId, int migratorMetaId, Task start)
    {
        var parameters = new DalParameterList();
        parameters.AddParameter(new DalParameter("ProductId", productId, typeof(int)));
        parameters.AddParameter(new DalParameter("EnvironmentId", environmentId, typeof(int)));
        parameters.AddParameter(new DalParameter("MigrationRunModeId", (byte)100, typeof(byte)));
        parameters.AddParameter(new DalParameter("MigratorMetaId", migratorMetaId, typeof(int)));
        parameters.AddParameter(new DalParameter("MigrationRunResultId", (byte)10, typeof(byte)));
        parameters.AddParameter(new DalParameter("FromReleaseVersion", null, typeof(string)));
        parameters.AddParameter(new DalParameter("ToReleaseVersion", null, typeof(string)));
        parameters.AddParameter(new DalParameter("MigrationRunSettingsJson", "{}", typeof(string)));
        parameters.AddParameter(new DalParameter("MigrationOperationId", (byte)100, typeof(byte)));
        parameters.AddParameter(new DalParameter("MigrationRunSourceId", null, typeof(byte)));
        parameters.AddParameter(new DalParameter("InitiatedBy", null, typeof(string)));
        parameters.AddParameter(new DalParameter("InitiatedBySubject", null, typeof(string)));
        parameters.AddParameter(new DalParameter("Host", "run-lock-test", typeof(string)));

        await start;
        try
        {
            var response = await TemplateExecutor.ExecuteScalarAsync(insert, dal, settings, parameters, TestContext.Current.CancellationToken);
            return new StartResult(response.ResultCode, null);
        }
        catch (TemplateResultException ex) when (ex.ResultCode == TemplateResultCode.MigrationAlreadyRunning)
        {
            return new StartResult(ex.ResultCode, null);
        }
        catch (Exception ex)
        {
            var cause = ex.GetBaseException();
            return new StartResult(0, $"{cause.GetType().Name}: {cause.Message}");
        }
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
