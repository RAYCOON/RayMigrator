using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Repository.ReadModel;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The contract the read model package promises on every engine (ADR-025): after a real migrate-up, every table of
/// the repository can be read with the package's statement through the engine DAL, every row maps to its record,
/// and the values are what the engine wrote. A column the templates name differently, or a provider type the mapper
/// does not convert, fails here.
/// </summary>
internal static class ReadModelContract
{
    private static readonly DalSettings Settings = new() { UseTransaction = false, DbCommandTimeoutInSeconds = 30, MaxRetries = 0 };

    public static async Task VerifyAfterMigrateUpAsync(RepositoryNaming naming, string connectionString, DateTime runStartedUtc)
    {
        DalFactory.TryGetDal(naming.DatabaseType, connectionString, out IDal? dal).Should().BeTrue();

        var rows = new Dictionary<RepositoryTable, List<object>>();
        foreach (var table in RepositoryReadModel.Tables)
        {
            var result = await dal!.ExecuteReaderAsync(naming.SelectStatement(table), Settings);
            rows[table] = result.Select(r => RepositoryRowMapper.Map(table, r, naming)).ToList();
        }

        var versions = rows[RepositoryTable.RepositorySchemaVersion].Cast<RepositorySchemaVersionRow>().ToList();
        versions.Max(v => v.Version).Should().Be(RepositoryReadModel.SchemaVersion, "the package describes the schema the engine created");
        versions.Should().HaveCount(RepositoryReadModel.SchemaVersion, "one row per applied step");
        versions.Should().AllSatisfy(v => v.RayMigratorVersion.Should().NotBeNullOrWhiteSpace());

        var meta = rows[RepositoryTable.MigratorMeta].Cast<MigratorMetaRow>().Should().ContainSingle().Subject;
        meta.RepositoryDatabaseType.Should().Be(naming.DatabaseType);
        meta.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);

        var product = rows[RepositoryTable.Product].Cast<ProductRow>().Should().ContainSingle().Subject;
        product.NameLower.Should().Be(product.Name.ToLowerInvariant());
        rows[RepositoryTable.Environment].Cast<EnvironmentRow>().Should().ContainSingle();

        var run = rows[RepositoryTable.MigrationRun].Cast<MigrationRunRow>().Should().ContainSingle().Subject;
        run.MigratorMetaId.Should().Be(meta.Id);
        run.ProductId.Should().Be(product.Id);
        run.MigrationRunModeId.Should().Be(MigrationRunMode.Migrate);
        run.MigrationRunResultId.Should().Be(MigrationRunResult.Ok);
        run.MigrationOperationId.Should().Be(MigrationOperation.MigrateUp);
        run.Host.Should().NotBeNullOrWhiteSpace("the engine records the machine name when the host names none");
        run.FinishedAt.Should().NotBeNull();
        run.StartedAt.Kind.Should().Be(DateTimeKind.Utc);
        run.StartedAt.Should().BeOnOrAfter(runStartedUtc.AddMinutes(-5)).And.BeOnOrBefore(DateTime.UtcNow.AddMinutes(5), "timestamps are UTC on every engine");
        run.FinishedAt!.Value.Should().BeOnOrAfter(run.StartedAt);
        run.DurationInMs.Should().BeGreaterThanOrEqualTo(0);
        run.ErrorCode.Should().BeNull();
        run.ErrorMessage.Should().BeNull();

        var runMeta = rows[RepositoryTable.MigrationRunMeta].Cast<MigrationRunMetaRow>().Should().ContainSingle().Subject;
        runMeta.MigrationRunId.Should().Be(run.Id);
        runMeta.MigrationRunSettingsJson.Should().NotBeNullOrWhiteSpace();

        var records = rows[RepositoryTable.MigrationRecord].Cast<MigrationRecordRow>().ToList();
        records.Should().NotBeEmpty();
        records.Should().AllSatisfy(r =>
        {
            r.MigrationRunId.Should().Be(run.Id);
            r.MigrationStatusId.Should().Be(MigrationStatus.Migrated);
            r.MigrationOperationId.Should().Be(MigrationOperation.MigrateUp);
            r.ReleaseVersion.Should().NotBeNullOrWhiteSpace();
            r.TargetAlias.Should().NotBeNullOrWhiteSpace();
            r.FileUpHash.Should().NotBeNullOrWhiteSpace();
            r.FileUpBlocksTotal.Should().BeGreaterThanOrEqualTo(r.FileUpBlocksMigrated);
            r.StartedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        });

        rows[RepositoryTable.MigrationRecordHistory].Cast<MigrationRecordHistoryRow>()
            .Should().AllSatisfy(h => h.HistorizedAt.Kind.Should().Be(DateTimeKind.Utc));

        AssertLookup<MigrationRunMode>(rows[RepositoryTable.MigrationRunMode]);
        AssertLookup<MigrationOperation>(rows[RepositoryTable.MigrationOperation]);
        AssertLookup<MigrationRunResult>(rows[RepositoryTable.MigrationRunResult]);
        AssertLookup<MigrationStatus>(rows[RepositoryTable.MigrationStatus]);
        var sources = AssertLookup<MigrationRunSource>(rows[RepositoryTable.MigrationRunSource]);
        sources.Select(s => (MigrationRunSource)s.Id).Should().Contain([MigrationRunSource.Cli, MigrationRunSource.Ci, MigrationRunSource.Studio]);

        rows[RepositoryTable.MigrationEvent].Cast<MigrationEventRow>().Should().NotBeEmpty("the log catalog is seeded with the log tables");

        var log = rows[RepositoryTable.MigrationLog].Cast<MigrationLogRow>().ToList();
        log.Should().NotBeEmpty("database logging goes into the repository by default");
        log.Should().AllSatisfy(l =>
        {
            ((int)l.LogLevelId).Should().BeGreaterThanOrEqualTo((int)LogLevel.Information, "Serilog passes Information and above in this scenario");
            l.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
            l.CreatedAt.Should().BeOnOrAfter(runStartedUtc.AddMinutes(-5)).And.BeOnOrBefore(DateTime.UtcNow.AddMinutes(5));
        });
        log.Should().Contain(l => l.MigrationRunId == run.Id, "entries written during the run carry its id");
    }

    private static List<LookupRow> AssertLookup<TEnum>(List<object> rows) where TEnum : struct, Enum
    {
        var lookup = rows.Cast<LookupRow>().ToList();
        lookup.Should().NotBeEmpty();
        lookup.Should().AllSatisfy(l =>
        {
            Enum.IsDefined((TEnum)Enum.ToObject(typeof(TEnum), l.Id)).Should().BeTrue($"[{l.Id}] must be a {typeof(TEnum).Name} member");
            l.Name.Should().NotBeNullOrWhiteSpace();
        });
        return lookup;
    }
}
