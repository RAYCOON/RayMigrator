using AwesomeAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <c>TemplateExecutor.MapMigrationRecord</c> maps the rows of the record select and the history select. Since 0.20.0
/// both return StartedAt and FinishedAt; the mapping reads them as UTC whatever the provider returns (a DateTime of any
/// kind, or text on SQLite), as <c>RepositoryRowMapper</c> of the read model does, and maps a row without the two
/// columns, as the select of an external DAL for an older engine returns it, with null dates instead of failing.
/// </summary>
public class MigrationRecordMappingTests
{
    private static Dictionary<string, object?> Row()
        => new()
        {
            ["Id"] = 5,
            ["ProductId"] = 1,
            ["MigrationRunId"] = 2,
            ["MigrationOperationId"] = (byte)MigrationOperation.MigrateUp,
            ["MigrationStatusId"] = (byte)MigrationStatus.Migrated,
            ["ReleaseVersion"] = "Release_1.0",
            ["TargetGroupAlias"] = "Backend",
            ["TargetAlias"] = "MainDB",
            ["Filename"] = "01_CreateTableA.sql",
            ["FileOrderId"] = 1,
            ["FileUpHash"] = "file-hash",
            ["FileUpConfigHash"] = null,
            ["FileUpBlocksHash"] = "blocks-hash",
            ["FileUpBlocksMigrated"] = 3,
            ["FileUpBlocksTotal"] = 3,
            ["MigrateDownFileExists"] = true,
            ["FileDownHash"] = null,
            ["FileDownConfigHash"] = null,
            ["FileDownBlocksHash"] = null,
            ["FileDownBlocksMigrated"] = null,
            ["FileDownBlocksTotal"] = null
        };

    private static Dictionary<string, object?> RowWithDates(object? startedAt, object? finishedAt)
    {
        var row = Row();
        row["StartedAt"] = startedAt;
        row["FinishedAt"] = finishedAt;
        return row;
    }

    [Fact]
    public void MapMigrationRecord_WithDates_ReadsThemAsUtc()
    {
        // Arrange: SQL Server, MySQL and MariaDB return UTC without a kind, PostgreSQL a timestamptz with the kind Utc
        var startedAt = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Unspecified);
        var finishedAt = new DateTime(2026, 10, 16, 8, 0, 5, DateTimeKind.Utc);

        // Act
        var record = TemplateExecutor.MapMigrationRecord(RowWithDates(startedAt, finishedAt));

        // Assert
        record.StartedAt.Should().Be(new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc));
        record.StartedAt!.Value.Kind.Should().Be(DateTimeKind.Utc, because: "the repository stores UTC on every engine, as the read model reads it");
        record.FinishedAt.Should().Be(finishedAt);
        record.FinishedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        record.Filename.Should().Be("01_CreateTableA.sql", because: "the other columns map as before");
        record.MigrationStatusId.Should().Be(MigrationStatus.Migrated);
    }

    [Fact]
    public void MapMigrationRecord_WithSqliteText_ParsesTheDatesAsUtc()
    {
        // Act
        var record = TemplateExecutor.MapMigrationRecord(RowWithDates("2026-10-16 08:00:00", "2026-10-16 08:00:05"));

        // Assert
        record.StartedAt.Should().Be(new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc));
        record.StartedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        record.FinishedAt.Should().Be(new DateTime(2026, 10, 16, 8, 0, 5, DateTimeKind.Utc));
        record.FinishedAt!.Value.Kind.Should().Be(DateTimeKind.Utc, because: "SQLite text holds UTC, the parse must not read it as local time");
    }

    [Fact]
    public void MapMigrationRecord_WithALocalDate_ConvertsItToUtc()
    {
        // Arrange
        var local = new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Local);

        // Act
        var record = TemplateExecutor.MapMigrationRecord(RowWithDates(local, null));

        // Assert
        record.StartedAt.Should().Be(local.ToUniversalTime());
        record.StartedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapMigrationRecord_WithNullFinishedAt_MapsNull(bool dbNull)
    {
        // Act: a record that is still executing has no end; a DAL may return DBNull instead of null
        var record = TemplateExecutor.MapMigrationRecord(RowWithDates(new DateTime(2026, 10, 16, 8, 0, 0), dbNull ? DBNull.Value : null));

        // Assert
        record.StartedAt.Should().NotBeNull();
        record.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void MapMigrationRecord_WithoutDateColumns_MapsNullDates()
    {
        // Act: the select of an external DAL written for an engine before 0.20.0
        var record = TemplateExecutor.MapMigrationRecord(Row());

        // Assert
        record.StartedAt.Should().BeNull(because: "a select without the column still maps");
        record.FinishedAt.Should().BeNull(because: "a select without the column still maps");
        record.Id.Should().Be(5);
        record.FileUpBlocksMigrated.Should().Be(3);
    }
}
