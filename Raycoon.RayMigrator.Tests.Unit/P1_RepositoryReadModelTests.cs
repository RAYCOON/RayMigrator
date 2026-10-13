using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Repository.ReadModel;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The technology-neutral read model of the repository (ADR-025): <see cref="RepositoryNaming"/> gives every
/// engine's table and column names, <see cref="RepositoryRows"/> derives the columns from the row records,
/// <see cref="RepositoryRowMapper"/> turns provider values into records, and the package names the schema version
/// of the engine it ships with. The engine suites prove the names against real repositories (ReadModelContractTests).
/// </summary>
public partial class RepositoryReadModelTests
{
    [Theory]
    [InlineData("MigrationRun", "migration_run")]
    [InlineData("RayMigratorVersion", "raymigrator_version")]
    [InlineData("RepositorySchemaVersion", "repository_schema_version")]
    [InlineData("FileUpConfigJson", "file_up_config_json")]
    [InlineData("DurationInMs", "duration_in_ms")]
    [InlineData("NameLower", "name_lower")]
    [InlineData("Id", "id")]
    [InlineData("MigrateDownFileExists", "migrate_down_file_exists")]
    public void ToSnakeCase_FollowsTheTemplateRule(string pascal, string snake)
    {
        RepositoryNaming.ToSnakeCase(pascal).Should().Be(snake);
        RepositoryQueryHelper.ToSnakeCase(pascal).Should().Be(snake, "the test helper delegates to the package");
    }

    [Theory]
    [InlineData("SqlServer", "[ray].[rm_MigrationRun]", "[MigrationRunId]")]
    [InlineData("PostgreSQL", "ray.rm_migration_run", "migration_run_id")]
    [InlineData("MariaDb", "`rm_migration_run`", "`migration_run_id`")]
    [InlineData("MySql", "`rm_migration_run`", "`migration_run_id`")]
    [InlineData("Sqlite", "\"rm_MigrationRun\"", "\"MigrationRunId\"")]
    public void Naming_QualifiesAndQuotesAsTheEngineTemplatesDo(string databaseType, string table, string column)
    {
        var naming = new RepositoryNaming(databaseType, "ray", "rm_");

        naming.QualifiedTableName(RepositoryTable.MigrationRun).Should().Be(table);
        naming.QualifiedColumnName("MigrationRunId").Should().Be(column);
    }

    [Fact]
    public void Naming_CanonicalisesTheDatabaseTypeAndDropsTheSchemaWhereTheEngineHasNone()
    {
        new RepositoryNaming("postgresql", "ray", null).DatabaseType.Should().Be("PostgreSQL");
        new RepositoryNaming("sqlite", "ignored", null).SchemaName.Should().BeNull();
        new RepositoryNaming("MySql", "ignored", "").SupportsSchema.Should().BeFalse();
        new RepositoryNaming("SqlServer", "dbo", null).TableName(RepositoryTable.MigrationLog).Should().Be("MigrationLog");

        var unknown = () => new RepositoryNaming("Oracle", null, null);
        unknown.Should().Throw<ArgumentException>().WithMessage("*Oracle*");
        var noSchema = () => new RepositoryNaming("PostgreSQL", " ", null);
        noSchema.Should().Throw<ArgumentException>().WithMessage("*schema*");
    }

    [Theory]
    [InlineData(RepositoryTable.MigrationRun, 19)]
    [InlineData(RepositoryTable.MigrationRecord, 28)]
    [InlineData(RepositoryTable.MigrationRecordHistory, 30)]
    [InlineData(RepositoryTable.MigrationLog, 16)]
    [InlineData(RepositoryTable.MigrationRunSource, 3)]
    [InlineData(RepositoryTable.RepositorySchemaVersion, 3)]
    public void Rows_ColumnsComeFromTheRecordConstructor(RepositoryTable table, int columns)
    {
        var names = RepositoryRows.ColumnsOf(table);

        names.Count.Should().Be(columns);
        names.Should().OnlyHaveUniqueItems();
        names.Should().AllSatisfy(n => PascalCase().IsMatch(n).Should().BeTrue($"[{n}] must be a PascalCase column name"));
    }

    [Fact]
    public void Rows_EveryTableHasARecordAndTheSchemaVersionIsTheEngines()
    {
        foreach (var table in RepositoryReadModel.Tables)
            RepositoryRows.TypeOf(table).Name.Should().EndWith("Row");

        RepositoryReadModel.Tables.Should().HaveCount(15);
        RepositoryReadModel.SchemaVersion.Should().Be(RepositorySchema.CurrentVersion);
        RepositoryRows.ColumnsOf(RepositoryTable.MigrationRun).Should().StartWith("Id").And.EndWith("LastHeartbeatAt");
    }

    [Fact]
    public void Naming_SelectStatement_ListsEveryColumnInRecordOrder()
    {
        var pg = new RepositoryNaming("PostgreSQL", "ray", "");
        pg.SelectStatement(RepositoryTable.MigrationRunSource).Should().Be("SELECT id, name, description FROM ray.migration_run_source");

        var sql = new RepositoryNaming("SqlServer", "ray", "");
        sql.SelectStatement(RepositoryTable.MigratorMeta)
            .Should().Be("SELECT [Id], [RayMigratorVersion], [RepositoryDatabaseType], [CreatedAt] FROM [ray].[MigratorMeta]");
    }

    [Fact]
    public void Mapper_SqliteValues_LongsTextDatesAndNulls()
    {
        var naming = new RepositoryNaming("Sqlite", null, null);
        var row = new Dictionary<string, object?>
        {
            ["Id"] = 7L, ["MigratorMetaId"] = 1L, ["ProductId"] = 2L, ["EnvironmentId"] = 3L,
            ["MigrationRunModeId"] = 100L, ["MigrationRunResultId"] = 100L,
            ["FromReleaseVersion"] = DBNull.Value, ["ToReleaseVersion"] = "2.0",
            ["StartedAt"] = "2026-10-13 10:00:00", ["FinishedAt"] = "2026-10-13 10:00:05", ["DurationInMs"] = 5000L,
            ["MigrationOperationId"] = 100L, ["MigrationRunSourceId"] = 10L,
            ["InitiatedBy"] = "daniel", ["InitiatedBySubject"] = DBNull.Value, ["Host"] = "mac",
            ["ErrorCode"] = null, ["ErrorMessage"] = DBNull.Value, ["LastHeartbeatAt"] = DBNull.Value,
        };

        var run = RepositoryRowMapper.Map<MigrationRunRow>(row, naming);

        run.Id.Should().Be(7);
        run.MigrationRunModeId.Should().Be(MigrationRunMode.Migrate);
        run.MigrationRunResultId.Should().Be(MigrationRunResult.Ok);
        run.MigrationOperationId.Should().Be(MigrationOperation.MigrateUp);
        run.MigrationRunSourceId.Should().Be(MigrationRunSource.Cli);
        run.FromReleaseVersion.Should().BeNull();
        run.StartedAt.Should().Be(new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc));
        run.StartedAt.Kind.Should().Be(DateTimeKind.Utc);
        run.FinishedAt.Should().Be(new DateTime(2026, 10, 13, 10, 0, 5, DateTimeKind.Utc));
        run.DurationInMs.Should().Be(5000);
        run.ErrorCode.Should().BeNull();
        run.LastHeartbeatAt.Should().BeNull();
    }

    [Fact]
    public void Mapper_ProviderTypedValues_ByteEnumsUnspecifiedDatesAndBooleans()
    {
        // SQL Server: tinyint → byte, bit → bool, datetime2 → DateTime without kind; keys as the provider reports them
        var naming = new RepositoryNaming("SqlServer", "ray", "");
        var row = new Dictionary<string, object?>
        {
            ["Id"] = 1, ["ProductId"] = 1, ["EnvironmentId"] = 1, ["MigrationRunId"] = 1,
            ["MigrationRunModeId"] = (byte)100, ["MigrationOperationId"] = (byte)100, ["MigrationStatusId"] = (byte)100,
            ["ReleaseVersion"] = "1.0", ["TargetGroupAlias"] = "Backend", ["TargetAlias"] = "Main", ["Filename"] = "01.sql",
            ["FileOrderId"] = 1, ["FileUpHash"] = "h", ["FileUpConfigHash"] = DBNull.Value, ["FileUpBlocksHash"] = "b",
            ["FileUpBlocksMigrated"] = 2, ["FileUpBlocksTotal"] = 2, ["FileUpConfigJson"] = DBNull.Value,
            ["MigrateDownFileExists"] = true, ["FileDownHash"] = DBNull.Value, ["FileDownConfigHash"] = DBNull.Value,
            ["FileDownBlocksHash"] = DBNull.Value, ["FileDownBlocksMigrated"] = DBNull.Value, ["FileDownBlocksTotal"] = DBNull.Value,
            ["FileDownConfigJson"] = DBNull.Value,
            ["StartedAt"] = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Unspecified), ["FinishedAt"] = DBNull.Value, ["DurationInMs"] = 12L,
        };

        var record = RepositoryRowMapper.Map<MigrationRecordRow>(row, naming);

        record.MigrationStatusId.Should().Be(MigrationStatus.Migrated);
        record.MigrateDownFileExists.Should().BeTrue();
        record.StartedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        record.FinishedAt.Should().BeNull();
        record.FileDownBlocksTotal.Should().BeNull();

        // MySQL: tinyint → sbyte, bool from 1, keys in lower case; the lookup is case-insensitive
        var mysql = new RepositoryNaming("MySql", null, "");
        var lookup = RepositoryRowMapper.Map<LookupRow>(new Dictionary<string, object?> { ["ID"] = (sbyte)30, ["NAME"] = "Studio", ["DESCRIPTION"] = null }, mysql);
        lookup.Id.Should().Be(30);
        RepositoryRowMapper.ConvertValue(1L, typeof(bool)).Should().Be(true);
        RepositoryRowMapper.ConvertValue("0", typeof(bool)).Should().Be(false);
        RepositoryRowMapper.ConvertValue((short)2, typeof(LogLevel)).Should().Be(LogLevel.Information);
    }

    [Fact]
    public void Mapper_MissingColumnAndNullForAValue_AreReportedByName()
    {
        var naming = new RepositoryNaming("PostgreSQL", "ray", "");
        var incomplete = new Dictionary<string, object?> { ["version"] = 2, ["applied_at"] = DateTime.UtcNow };
        var missing = () => RepositoryRowMapper.Map<RepositorySchemaVersionRow>(incomplete, naming);
        missing.Should().Throw<KeyNotFoundException>().WithMessage("*raymigrator_version*RepositorySchemaVersionRow.RayMigratorVersion*");

        var nullVersion = new Dictionary<string, object?> { ["version"] = DBNull.Value, ["applied_at"] = DateTime.UtcNow, ["raymigrator_version"] = "0.17.0" };
        var nulled = () => RepositoryRowMapper.Map(RepositoryTable.RepositorySchemaVersion, nullVersion, naming);
        nulled.Should().Throw<InvalidCastException>().WithMessage("*RepositorySchemaVersionRow.Version*cannot be null*");
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalCase();
}
