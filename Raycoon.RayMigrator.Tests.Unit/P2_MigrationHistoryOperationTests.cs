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
