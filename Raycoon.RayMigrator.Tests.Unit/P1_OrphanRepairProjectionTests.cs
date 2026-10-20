using System.Reflection;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The status projects the orphan repair of the next migrate-up onto the open records of the open run
/// (<see cref="MigrationService.ProjectOrphanRepair"/>) with the rule of <c>Repository_MigrationRecord_FixOrphaned</c>: a
/// rollback that was running ends NotMigrated when every rollback block ran and Failed otherwise; a file ends Migrated when
/// every block committed, Failed after some committed blocks and NotMigrated without any. The counters stay, and records
/// of other runs and closed records are left alone.
/// </summary>
public class OrphanRepairProjectionTests
{
    private const int OpenRunId = 7;

    private static MigrationRecord Record(
        MigrationStatus status, int blocksMigrated, string? fileDownHash = null, int? downMigrated = null, int? downTotal = null,
        int runId = OpenRunId, int id = 1)
    {
        var record = TestFactories.CreateMigrationRecord(id: id, status: status, fileUpBlocksMigrated: blocksMigrated,
            fileUpBlocksTotal: 3, fileDownHash: fileDownHash, fileDownBlocksMigrated: downMigrated, fileDownBlocksTotal: downTotal);
        record.MigrationRunId = runId;
        return record;
    }

    public static TheoryData<MigrationStatus, int, string?, int?, int?, MigrationStatus> RepairRules() => new()
    {
        // A rollback was running and every rollback block ran: only the final status was missing
        { MigrationStatus.Executing, 3, "down", 3, 3, MigrationStatus.NotMigrated },
        // A rollback was running after some rollback blocks: migrate-down continues it
        { MigrationStatus.Executing, 3, "down", 1, 3, MigrationStatus.Failed },
        // A rollback was running in its first block: nothing to continue
        { MigrationStatus.Executing, 3, "down", 0, 3, MigrationStatus.Failed },
        { MigrationStatus.Executing, 0, "down", null, null, MigrationStatus.Failed },
        // Every block committed, only the final status was missing
        { MigrationStatus.Executing, 3, null, null, null, MigrationStatus.Migrated },
        // Some blocks committed: the next migrate-up resumes after them
        { MigrationStatus.Executing, 1, null, null, null, MigrationStatus.Failed },
        { MigrationStatus.Executing, 2, null, null, null, MigrationStatus.Failed },
        // No block committed
        { MigrationStatus.Executing, 0, null, null, null, MigrationStatus.NotMigrated },
        { MigrationStatus.Pending, 0, null, null, null, MigrationStatus.NotMigrated },
    };

    [Theory]
    [MemberData(nameof(RepairRules))]
    public void Project_OpenRecordOfTheOpenRun_EndsAsTheRepairLeavesIt(
        MigrationStatus status, int blocksMigrated, string? fileDownHash, int? downMigrated, int? downTotal, MigrationStatus expected)
    {
        // Arrange
        var record = Record(status, blocksMigrated, fileDownHash, downMigrated, downTotal);

        // Act
        var projected = MigrationService.ProjectOrphanRepair([record], OpenRunId);

        // Assert
        projected.Should().ContainSingle().Which.MigrationStatusId.Should().Be(expected);
        projected[0].FileUpBlocksMigrated.Should().Be(blocksMigrated, because: "the repair keeps the counters");
        projected[0].FileDownBlocksMigrated.Should().Be(downMigrated);
    }

    [Fact]
    public void Project_KeepsEveryOtherValueOfTheRecord()
    {
        // Arrange: every property of the record carries a value other than its default, so that a copy that leaves one
        // out differs from the record; a property of a type without a sample value fails the test until it gets one.
        // The record is open in the open run with a rollback after some of its rollback blocks, so FileDownHash keeps
        // its value as well.
        var record = new MigrationRecord();
        foreach (var property in typeof(MigrationRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            property.SetValue(record, SampleValue(property.PropertyType, property.Name));
        }

        record.MigrationRunId = OpenRunId;
        record.MigrationStatusId = MigrationStatus.Executing;
        record.FileDownBlocksMigrated = 1;
        record.FileDownBlocksTotal = 3;

        // Act
        var projected = MigrationService.ProjectOrphanRepair([record], OpenRunId).Should().ContainSingle().Which;

        // Assert
        projected.Should().NotBeSameAs(record, because: "the projection must not change the records the status read");
        record.MigrationStatusId.Should().Be(MigrationStatus.Executing);
        projected.MigrationStatusId.Should().Be(MigrationStatus.Failed);
        foreach (var property in typeof(MigrationRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.Name != nameof(MigrationRecord.MigrationStatusId)))
        {
            property.GetValue(projected).Should().Be(property.GetValue(record), because: $"the projection copies {property.Name}");
        }
    }

    [Fact]
    public void Project_LeavesRecordsOfOtherRunsAndClosedRecordsAlone()
    {
        // Arrange
        var otherRun = Record(MigrationStatus.Executing, 1, runId: 3, id: 1);
        var failed = Record(MigrationStatus.Failed, 1, id: 2);
        var migrated = Record(MigrationStatus.Migrated, 3, id: 3);
        var notMigrated = Record(MigrationStatus.NotMigrated, 0, id: 4);
        List<MigrationRecord> records = [otherRun, failed, migrated, notMigrated];

        // Act
        var projected = MigrationService.ProjectOrphanRepair(records, OpenRunId);

        // Assert
        projected.Should().Equal(records, (a, b) => ReferenceEquals(a, b), because: "the repair settles only the Pending and Executing records of the run it closes");
    }

    [Fact]
    public void Project_WithoutAnOpenRun_ReturnsTheRecordsAsTheyAre()
    {
        // Arrange
        List<MigrationRecord> records = [Record(MigrationStatus.Executing, 1)];

        // Act
        var projected = MigrationService.ProjectOrphanRepair(records, null);

        // Assert
        projected.Should().Equal(records, (a, b) => ReferenceEquals(a, b));
    }

    private static object? SampleValue(Type type, string name) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(int) => name.Length + 11,
        var t when t == typeof(string) => name + "-value",
        var t when t == typeof(bool) => true,
        var t when t == typeof(DateTime) => new DateTime(2026, 10, 16, 12, name.Length % 60, 0, DateTimeKind.Utc),
        var t when t == typeof(MigrationStatus) => MigrationStatus.Executing,
        var t when t == typeof(MigrationOperation) => MigrationOperation.MigrateUp,
        var t => throw new InvalidOperationException($"No sample value for {t.Name} ({name}); extend the test with the new property type")
    };
}
