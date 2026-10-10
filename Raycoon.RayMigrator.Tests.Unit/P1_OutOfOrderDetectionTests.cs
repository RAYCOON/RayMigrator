using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-3: Out-of-Order migration detection tests.
/// DetectOutOfOrderFiles identifies pending files from releases older than the highest already-migrated release.
/// Errors here could silently skip migrations or incorrectly block valid migrations.
/// </summary>
public class DetectOutOfOrderFilesTests
{
    [Fact]
    public void NoExistingRecords_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile() };
        var records = new List<MigrationRecord>();

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void NoFilesToMigrate_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>();
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord() };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllFilesFromNewerRelease_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 2.0"),
            TestFactories.CreateMigrationFile(filename: "20_B.sql", release: "Release 2.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllFilesFromSameRelease_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "20_New.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FileFromOlderRelease_IsDetected()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().HaveCount(1);
        result[0].Filename.Should().Be("10_Missed.sql");
    }

    [Fact]
    public void MixedReleasesFiles_OnlyOlderDetected()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Old.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationFile(filename: "20_New.sql", release: "Release 2.0"),
            TestFactories.CreateMigrationFile(filename: "30_Newer.sql", release: "Release 3.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().HaveCount(1);
        result[0].Filename.Should().Be("10_Old.sql");
    }

    [Fact]
    public void MultipleOlderReleases_AllDetected()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationFile(filename: "10_B.sql", release: "Release 1.1"),
            TestFactories.CreateMigrationFile(filename: "10_C.sql", release: "Release 1.2")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void OnlyUnclearRecords_ReturnsEmpty()
    {
        // Records that are not in Migrated/Ok state should not count
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0",
                status: MigrationStatus.Failed)
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void OnlyErrorRecords_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0",
                status: MigrationStatus.Pending)
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void HighestReleaseDeterminedByStringComparison()
    {
        // "Release 2.0" > "Release 10.0" alphabetically — tests the comparison behavior
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 10.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_R1.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationRecord(filename: "10_R2.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        // "Release 2.0" is highest alphabetically, "Release 10.0" < "Release 2.0" alphabetically
        result.Should().HaveCount(1);
    }

    [Fact]
    public void MultipleSuccessfulRecords_UsesHighestRelease()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.5")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_R1.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationRecord(filename: "10_R2.sql", release: "Release 2.0"),
            TestFactories.CreateMigrationRecord(filename: "10_R3.sql", release: "Release 1.5")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        // Highest is "Release 2.0", "Release 1.5" < "Release 2.0" => out of order
        result.Should().HaveCount(1);
    }

    // === #8: out-of-order is evaluated per target ===

    [Fact]
    public void LaggingTarget_CatchingUpOnReleasesItHasNeverSeen_IsNotOutOfOrder()
    {
        // MainDB is at Release 4.0, SecondDB only at Release 1.0; the Release 2.0 file is pending on SecondDB only.
        var file = TestFactories.CreateMigrationFile(filename: "20_Create.sql", release: "Release 2.0");
        file.PendingTargetAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SecondDB" };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "20_Create.sql", release: "Release 2.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "40_Create.sql", release: "Release 4.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0", targetAlias: "SecondDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().BeEmpty("Release 2.0 is newer than everything SecondDB has migrated, so it is in order for that target (#8)");
    }

    [Fact]
    public void FilePendingOnTargetThatIsAlreadyBeyondItsRelease_IsOutOfOrder()
    {
        var file = TestFactories.CreateMigrationFile(filename: "15_Create.sql", release: "Release 1.5");
        file.PendingTargetAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SecondDB" };
        var records = new List<MigrationRecord>
        {
            // MainDB is only at Release 1.0 and must not mask SecondDB's higher release
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0", targetAlias: "SecondDB"),
            TestFactories.CreateMigrationRecord(filename: "20_Create.sql", release: "Release 2.0", targetAlias: "SecondDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().ContainSingle("SecondDB has already migrated Release 2.0, so a new Release 1.5 file is out of order for it");
    }

    [Fact]
    public void FilePendingOnAllTargets_IsOutOfOrderWhenAnyTargetIsBeyondItsRelease()
    {
        var file = TestFactories.CreateMigrationFile(filename: "15_Create.sql", release: "Release 1.5");
        file.PendingTargetAliases = null;
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "20_Create.sql", release: "Release 2.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0", targetAlias: "SecondDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().ContainSingle("MainDB is beyond Release 1.5 and the file is pending there too");
    }

    // === #10: targets outside a file's Targets filter do not make it out of order ===

    [Fact]
    public void TargetsFilter_HigherReleaseOnlyOnAnUnselectedTarget_IsNotOutOfOrder()
    {
        var file = TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 1.0");
        file.Targets = new List<string> { "SecondDB" };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "99_Z.sql", release: "Release 2.0", targetAlias: "MainDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().BeEmpty("MainDB is not selected by the file's Targets filter (#10)");
    }

    [Fact]
    public void TargetsFilter_HigherReleaseOnTheSelectedTarget_IsOutOfOrder()
    {
        var file = TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 1.0");
        file.Targets = new List<string> { "SecondDB" };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "99_Z.sql", release: "Release 2.0", targetAlias: "SecondDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().ContainSingle();
    }
    // === #25: RunAlways files are pending by design and never out of order on a target that has received them ===

    [Fact]
    public void RunAlwaysFile_RecordOnTheTargetBeyondItsRelease_IsNotOutOfOrder()
    {
        var file = TestFactories.CreateMigrationFile(filename: "90_View.sql", release: "Release 1.0", runAlways: true);
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "90_View.sql", release: "Release 1.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetAlias: "MainDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().BeEmpty("MainDB has already received the RunAlways file; re-running it is its purpose (#25)");
    }

    [Theory]
    [InlineData(MigrationStatus.Failed)]
    [InlineData(MigrationStatus.NotMigrated)]
    public void RunAlwaysFile_NonMigratedRecordOnTheTarget_IsNotOutOfOrder(MigrationStatus status)
    {
        // After a failed re-run or a rollback the current record is Failed or NotMigrated; the file still re-runs.
        var file = TestFactories.CreateMigrationFile(filename: "90_View.sql", release: "Release 1.0", runAlways: true);
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "90_View.sql", release: "Release 1.0", targetAlias: "MainDB", status: status),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetAlias: "MainDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().BeEmpty($"a {status} record proves MainDB has received the RunAlways file before (#25)");
    }

    [Fact]
    public void RunAlwaysFile_NoRecordOnATargetBeyondItsRelease_IsOutOfOrder()
    {
        var file = TestFactories.CreateMigrationFile(filename: "90_View.sql", release: "Release 1.0", runAlways: true);
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetAlias: "MainDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().ContainSingle("a RunAlways file newly added to an old release is out of order like any other new file (#25)");
    }

    [Fact]
    public void RunAlwaysFile_RecordOnOneTargetOnly_IsOutOfOrderForTheOtherTarget()
    {
        var file = TestFactories.CreateMigrationFile(filename: "90_View.sql", release: "Release 1.0", runAlways: true);
        file.PendingTargetAliases = null;
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "90_View.sql", release: "Release 1.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetAlias: "SecondDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().ContainSingle("SecondDB is beyond Release 1.0 and has never received the RunAlways file (#25)");
    }

    [Fact]
    public void RunAlwaysFile_RecordFromAnotherRelease_DoesNotCount()
    {
        var file = TestFactories.CreateMigrationFile(filename: "90_View.sql", release: "Release 1.0", runAlways: true);
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "90_View.sql", release: "Release 0.9", targetAlias: "MainDB"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetAlias: "MainDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().ContainSingle("the record belongs to another release, so the file in Release 1.0 is new to MainDB (#25)");
    }

    [Fact]
    public void RunAlwaysFile_TargetAliasMatchIsCaseInsensitive()
    {
        var file = TestFactories.CreateMigrationFile(filename: "90_View.sql", release: "Release 1.0", runAlways: true);
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "90_View.sql", release: "Release 1.0", targetAlias: "maindb"),
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetAlias: "MainDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().BeEmpty("target aliases are compared case-insensitively, as in IsAppliedOnTarget (#25)");
    }

    [Fact]
    public void NonRunAlwaysFile_HashMismatchRecord_IsStillOutOfOrder()
    {
        // A changed file in an older release stays pending with a hash mismatch and must still trip the guard.
        var file = TestFactories.CreateMigrationFile(filename: "15_Create.sql", release: "Release 1.5", hash: "new");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "15_Create.sql", release: "Release 1.5", targetAlias: "MainDB", hash: "old"),
            TestFactories.CreateMigrationRecord(filename: "20_Create.sql", release: "Release 2.0", targetAlias: "MainDB")
        };

        var result = MigrationService.DetectOutOfOrderFiles(new List<MigrationFileInfo> { file }, records);

        result.Should().ContainSingle("the RunAlways exemption of #25 does not apply to ordinary files");
    }
}
