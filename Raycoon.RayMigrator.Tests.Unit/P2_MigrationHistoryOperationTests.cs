using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Baseline runs are named after what they did (#6). <see cref="MigrationOperation.Baseline"/> exists, and
/// <see cref="MigrationService.DeriveRunOperation"/> (used by <c>GetHistoryAsync</c> for the <c>info</c> run history)
/// reports it for runs whose records were written by <c>baseline</c>.
/// </summary>
public class MigrationHistoryOperationTests
{
    [Fact]
    public void MigrationOperation_HasBaseline_DistinctFromMigrateUp()
    {
        Enum.IsDefined(MigrationOperation.Baseline).Should().BeTrue();
        MigrationOperation.Baseline.Should().NotBe(MigrationOperation.MigrateUp);
        ((byte)MigrationOperation.Baseline).Should().Be(110, "the value is seeded into the MigrationOperation lookup table of every DAL");
    }

    [Fact]
    public void DeriveRunOperation_ForBaselineRecords_ReturnsBaseline()
    {
        var records = Records(MigrationOperation.Baseline, MigrationOperation.Baseline);

        MigrationService.DeriveRunOperation(records).Should().Be(MigrationOperation.Baseline);
    }

    [Fact]
    public void DeriveRunOperation_ForMigrateDownRecords_ReturnsMigrateDown()
    {
        var records = Records(MigrationOperation.MigrateDown, MigrationOperation.MigrateUp);

        MigrationService.DeriveRunOperation(records).Should().Be(MigrationOperation.MigrateDown);
    }

    [Fact]
    public void DeriveRunOperation_ForMigrateUpRecords_ReturnsMigrateUp()
    {
        var records = Records(MigrationOperation.MigrateUp, MigrationOperation.MigrateUp);

        MigrationService.DeriveRunOperation(records).Should().Be(MigrationOperation.MigrateUp);
    }

    [Fact]
    public void DeriveRunOperation_ForRollbackRecordsOnly_ReturnsMigrateUp()
    {
        // Error-recovery rollbacks happen inside a migrate-up run; the run stays a MigrateUp run.
        var records = Records(MigrationOperation.Rollback);

        MigrationService.DeriveRunOperation(records).Should().Be(MigrationOperation.MigrateUp);
    }

    [Fact]
    public void DeriveRunOperation_WithoutRecords_ReturnsMigrateUp()
    {
        MigrationService.DeriveRunOperation(Array.Empty<MigrationRecord>()).Should().Be(MigrationOperation.MigrateUp,
            "a run without records (nothing pending) is an up-migration that had nothing to do");
    }

    [Fact]
    public void SummarizeRun_MigrateDown_CountsRolledBackRecordsAsSuccessful()
    {
        // history rows of a migrate-down run: three records rolled back, one rollback failed (#13)
        var rows = new List<MigrationRecord>
        {
            History(1, MigrationOperation.MigrateDown, MigrationStatus.NotMigrated),
            History(2, MigrationOperation.MigrateDown, MigrationStatus.NotMigrated),
            History(3, MigrationOperation.MigrateDown, MigrationStatus.Failed),
        };

        var summary = MigrationService.SummarizeRun(rows);

        summary.Should().Be((3, 2, 1, MigrationOperation.MigrateDown));
    }

    [Fact]
    public void SummarizeRun_MigrateUpWithErrorRecovery_UsesTheLastTransitionOfEachRecord()
    {
        // migrate-up migrated two records, the error recovery rolled both back again: the run stays MigrateUp,
        // it touched two records, none of them ended Migrated (#13)
        var rows = new List<MigrationRecord>
        {
            History(1, MigrationOperation.MigrateUp, MigrationStatus.Migrated),
            History(2, MigrationOperation.MigrateUp, MigrationStatus.Failed),
            History(2, MigrationOperation.Rollback, MigrationStatus.NotMigrated),
            History(1, MigrationOperation.Rollback, MigrationStatus.NotMigrated),
        };

        var summary = MigrationService.SummarizeRun(rows);

        summary.Should().Be((2, 0, 0, MigrationOperation.MigrateUp));
    }

    [Fact]
    public void SummarizeRun_WithoutRows_IsAnEmptyMigrateUp()
    {
        MigrationService.SummarizeRun(Array.Empty<MigrationRecord>()).Should().Be((0, 0, 0, MigrationOperation.MigrateUp));
    }

    private static MigrationRecord History(int recordId, MigrationOperation operation, MigrationStatus status) => new()
    {
        Id = recordId,
        MigrationRunId = 7,
        MigrationOperationId = operation,
        MigrationStatusId = status,
        Filename = $"{recordId:00}_file.sql",
        ReleaseVersion = "Release_1.0",
        TargetGroupAlias = "Backend",
        TargetAlias = "MainDB"
    };

    private static List<MigrationRecord> Records(params MigrationOperation[] operations)
        => operations.Select((op, i) => new MigrationRecord
        {
            Id = i + 1,
            MigrationRunId = 7,
            MigrationOperationId = op,
            MigrationStatusId = MigrationStatus.Migrated,
            Filename = $"{i + 1:00}_file.sql",
            ReleaseVersion = "Release_1.0",
            TargetGroupAlias = "Backend",
            TargetAlias = "MainDB"
        }).ToList();
}
