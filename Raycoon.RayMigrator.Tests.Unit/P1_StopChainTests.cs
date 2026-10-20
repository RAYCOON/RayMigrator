using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the rollback chain of a stopped migrate-up (<see cref="MigrationService.BuildStopChain"/>), for an error action and a
/// cancel action alike: the stopped file first when it has a record, Failed with the blocks it committed, then the records
/// the run wrote, newest first, each as the run left it: the files it migrated or finalized, and the files it left Failed
/// under MigrationErrorAction=Ignore with the blocks they committed, also none. Each record carries the position and the
/// blocks of its file. Rollback takes every record of the run, RollbackRelease those of the stopped file's release (without
/// a stopped file the release of the record the run wrote last), RollbackErrorOnly none of them, and an action that rolls
/// nothing back builds no chain.
/// </summary>
public class StopChainTests
{
    private const string Release1 = "Release 1.0";
    private const string Release2 = "Release 2.0";
    private const string MainDb = "MainDB";
    private const string SecondDb = "SecondDB";

    /// <summary>A file with three blocks, at <paramref name="fileOrderId"/> in the order of the run.</summary>
    private static MigrationFileInfo ThreeBlockFile(string filename, string release, int fileOrderId = 0)
    {
        var file = TestFactories.CreateMigrationFile(filename: filename, release: release, sqlBlocks: ["block 1", "block 2", "block 3"]);
        file.FileOrderId = fileOrderId;
        return file;
    }

    /// <summary>The record of a file the run migrated or finalized on <paramref name="target"/>.</summary>
    private static MigrationService.RunRecord Migrated(MigrationFileInfo file, int id, string target = MainDb)
        => new(file, id, target, MigrationStatus.Migrated, file.FileUpBlocksTotal);

    /// <summary>The record of a file the run left Failed under MigrationErrorAction=Ignore with <paramref name="committedBlocks"/>.</summary>
    private static MigrationService.RunRecord LeftFailedUnderIgnore(MigrationFileInfo file, int id, int committedBlocks)
        => new(file, id, MainDb, MigrationStatus.Failed, committedBlocks);

    /// <summary>The chain as (file, target, record, status, committed blocks), in its order.</summary>
    private static List<(string File, string Target, int Id, MigrationStatus Status, int Committed)> Shape(List<MigrationRecord>? chain)
    {
        chain.Should().NotBeNull(because: "the action rolls back");
        return chain!.Select(r => (r.Filename, r.TargetAlias, r.Id, r.MigrationStatusId, r.FileUpBlocksMigrated)).ToList();
    }

    [Fact]
    public void BuildStopChain_Rollback_TakesTheStoppedFileThenEveryRecordOfTheRunNewestFirst()
    {
        // Arrange: on two targets the run migrated 01, then left 02 Failed under Ignore with block 1 on the first target, which
        // the second target skipped, and migrated 03 on the first target; 04 failed after 2 committed blocks
        var createTableA = ThreeBlockFile("01_CreateTableA.sql", Release1);
        var seed = ThreeBlockFile("02_Seed.sql", Release1);
        var createTableB = ThreeBlockFile("03_CreateTableB.sql", Release1);
        var stopped = ThreeBlockFile("04_CreateTableC.sql", Release1);
        List<MigrationService.RunRecord> records =
            [Migrated(createTableA, 1), Migrated(createTableA, 2, SecondDb), LeftFailedUnderIgnore(seed, 3, 1), Migrated(createTableB, 4)];

        // Act
        var chain = MigrationService.BuildStopChain(MigrationErrorAction.Rollback, stopped, 5, MainDb, 2, records);

        // Assert
        Shape(chain).Should().Equal(
            [("04_CreateTableC.sql", MainDb, 5, MigrationStatus.Failed, 2),
             ("03_CreateTableB.sql", MainDb, 4, MigrationStatus.Migrated, 3),
             ("02_Seed.sql", MainDb, 3, MigrationStatus.Failed, 1),
             ("01_CreateTableA.sql", SecondDb, 2, MigrationStatus.Migrated, 3),
             ("01_CreateTableA.sql", MainDb, 1, MigrationStatus.Migrated, 3)],
            because: "the chain rolls back the stopped file first and then every record the run wrote, newest first, the file it left Failed under Ignore with its committed block");
        chain!.Should().AllSatisfy(r =>
        {
            r.ReleaseVersion.Should().Be(Release1);
            r.TargetGroupAlias.Should().Be("Backend");
        });
    }

    [Fact]
    public void BuildStopChain_FileLeftFailedUnderIgnoreWithoutACommittedBlock_IsInTheChain()
    {
        // Arrange: block 1 of the seed failed under Ignore and blocks 2 and 3 ran after it, or a CLI tool ran the seed and
        // failed under Ignore: the record counts no committed block, while the target may hold what the seed wrote; the run
        // is cancelled between two files, so there is no stopped file
        var createTableA = ThreeBlockFile("01_CreateTableA.sql", Release1);
        var seed = ThreeBlockFile("02_Seed.sql", Release1);
        List<MigrationService.RunRecord> records = [Migrated(createTableA, 1), LeftFailedUnderIgnore(seed, 2, 0)];

        // Act
        var chain = MigrationService.BuildStopChain(MigrationErrorAction.Rollback, null, 0, string.Empty, 0, records);

        // Assert
        Shape(chain).Should().Equal(
            [("02_Seed.sql", MainDb, 2, MigrationStatus.Failed, 0),
             ("01_CreateTableA.sql", MainDb, 1, MigrationStatus.Migrated, 3)],
            because: "the chain rolls back every file the run left Failed under Ignore, also one whose record counts no committed block");
    }

    [Fact]
    public void BuildStopChain_RollbackRelease_TakesTheRecordsOfTheStoppedFilesReleaseOnly()
    {
        // Arrange: release 1.0 migrated 01 and left its seed Failed under Ignore with block 1; release 2.0 migrated 01 and
        // left 02 Failed under Ignore with block 1; the seed of release 2.0 stopped without a committed block
        var createTableA = ThreeBlockFile("01_CreateTableA.sql", Release1);
        var seedA = ThreeBlockFile("03_SeedDataA.sql", Release1);
        var createTableC = ThreeBlockFile("01_CreateTableC.sql", Release2);
        var createTableD = ThreeBlockFile("02_CreateTableD.sql", Release2);
        var seedB = ThreeBlockFile("03_SeedDataB.sql", Release2);
        List<MigrationService.RunRecord> records =
            [Migrated(createTableA, 1), LeftFailedUnderIgnore(seedA, 2, 1), Migrated(createTableC, 3), LeftFailedUnderIgnore(createTableD, 4, 1)];

        // Act
        var chain = MigrationService.BuildStopChain(MigrationErrorAction.RollbackRelease, seedB, 5, MainDb, 0, records);

        // Assert
        Shape(chain).Should().Equal(
            [("03_SeedDataB.sql", MainDb, 5, MigrationStatus.Failed, 0),
             ("02_CreateTableD.sql", MainDb, 4, MigrationStatus.Failed, 1),
             ("01_CreateTableC.sql", MainDb, 3, MigrationStatus.Migrated, 3)],
            because: "RollbackRelease takes the records of release 2.0 the run wrote, and the seed of release 1.0 keeps its block");
    }

    [Fact]
    public void BuildStopChain_RollbackReleaseWithoutAStoppedFile_TakesTheReleaseOfTheRecordTheRunWroteLast()
    {
        // Arrange: a cancel between two files, after the run left 02 of release 2.0 Failed under Ignore without a committed
        // block; that is the record it wrote last
        var createTableA = ThreeBlockFile("01_CreateTableA.sql", Release1);
        var createTableC = ThreeBlockFile("01_CreateTableC.sql", Release2);
        var createTableD = ThreeBlockFile("02_CreateTableD.sql", Release2);
        List<MigrationService.RunRecord> records = [Migrated(createTableA, 1), Migrated(createTableC, 2), LeftFailedUnderIgnore(createTableD, 3, 0)];

        // Act
        var chain = MigrationService.BuildStopChain(MigrationErrorAction.RollbackRelease, null, 0, string.Empty, 0, records);

        // Assert
        Shape(chain).Should().Equal(
            [("02_CreateTableD.sql", MainDb, 3, MigrationStatus.Failed, 0),
             ("01_CreateTableC.sql", MainDb, 2, MigrationStatus.Migrated, 3)],
            because: "without a stopped file the release is that of the record the run wrote last, also one it left Failed under Ignore");
    }

    [Fact]
    public void BuildStopChain_RollbackErrorOnly_TakesTheStoppedFileOnly()
    {
        // Arrange: the run migrated 01 and left 02 Failed under Ignore with block 1; 03 failed after 1 committed block
        var createTableA = ThreeBlockFile("01_CreateTableA.sql", Release1);
        var seed = ThreeBlockFile("02_Seed.sql", Release1);
        var stopped = ThreeBlockFile("03_CreateTableB.sql", Release1);
        List<MigrationService.RunRecord> records = [Migrated(createTableA, 1), LeftFailedUnderIgnore(seed, 2, 1)];

        // Act
        var chain = MigrationService.BuildStopChain(MigrationErrorAction.RollbackErrorOnly, stopped, 3, MainDb, 1, records);

        // Assert
        Shape(chain).Should().Equal([("03_CreateTableB.sql", MainDb, 3, MigrationStatus.Failed, 1)],
            because: "RollbackErrorOnly rolls back the stopped file and leaves the other records of the run as they are");
    }

    [Fact]
    public void BuildStopChain_EachRecord_CarriesThePositionAndTheBlocksOfItsFile()
    {
        // Arrange: the run migrated 01, a file of two blocks, and left 02 Failed under Ignore with block 1; 03 failed after 1
        // committed block
        var createTableA = TestFactories.CreateMigrationFile(filename: "01_CreateTableA.sql", release: Release1, sqlBlocks: ["block 1", "block 2"]);
        createTableA.FileOrderId = 4;
        var seed = ThreeBlockFile("02_Seed.sql", Release1, fileOrderId: 7);
        var stopped = ThreeBlockFile("03_CreateTableB.sql", Release1, fileOrderId: 9);
        List<MigrationService.RunRecord> records = [Migrated(createTableA, 1), LeftFailedUnderIgnore(seed, 2, 1)];

        // Act
        var chain = MigrationService.BuildStopChain(MigrationErrorAction.Rollback, stopped, 3, MainDb, 1, records);

        // Assert
        chain.Should().NotBeNull(because: "the action rolls back");
        chain!.Select(r => (r.Filename, r.FileOrderId, r.FileUpBlocksTotal)).Should().Equal(
            [("03_CreateTableB.sql", 9, 3), ("02_Seed.sql", 7, 3), ("01_CreateTableA.sql", 4, 2)],
            because: "the rollback events of the chain report the position of each file in the run and, when its rollback file is missing, its blocks");
    }

    [Theory]
    [InlineData(MigrationErrorAction.Rollback, true)]
    [InlineData(MigrationErrorAction.RollbackRelease, true)]
    [InlineData(MigrationErrorAction.RollbackErrorOnly, false)]
    public void BuildStopChain_StoppedFileWithoutARecord_IsNotInTheChain(MigrationErrorAction action, bool takesTheRecordsOfTheRun)
    {
        // Arrange: the run migrated 01; 02 failed before the insert of its record, so it ran nothing on its target, while the
        // state of the run still names the target of 01 and its three committed blocks
        var stopped = ThreeBlockFile("02_CreateTableB.sql", Release1);
        List<MigrationService.RunRecord> records = [Migrated(ThreeBlockFile("01_CreateTableA.sql", Release1), 1)];
        List<(string File, string Target, int Id, MigrationStatus Status, int Committed)> expected =
            takesTheRecordsOfTheRun ? [("01_CreateTableA.sql", MainDb, 1, MigrationStatus.Migrated, 3)] : [];

        // Act
        var chain = MigrationService.BuildStopChain(action, stopped, 0, MainDb, 3, records);

        // Assert
        Shape(chain).Should().Equal(expected,
            because: "a file without a record ran nothing on its target, and the chain rolls back the files the run ran in the scope of the action");
    }

    [Theory]
    [InlineData(MigrationErrorAction.Rollback, false)]
    [InlineData(MigrationErrorAction.RollbackRelease, false)]
    [InlineData(MigrationErrorAction.RollbackErrorOnly, true)]
    public void BuildStopChain_NothingInTheScopeOfTheAction_IsEmpty(MigrationErrorAction action, bool runWroteARecord)
    {
        // Arrange: a cancel before the first file wrote no record; a cancel between two files has no stopped file, which is
        // all RollbackErrorOnly takes
        List<MigrationService.RunRecord> records = runWroteARecord ? [Migrated(ThreeBlockFile("01_CreateTableA.sql", Release1), 1)] : [];

        // Act
        var chain = MigrationService.BuildStopChain(action, null, 0, string.Empty, 0, records);

        // Assert
        chain.Should().NotBeNull(because: "the action rolls back, it only has nothing to roll back").And.BeEmpty();
    }

    [Theory]
    [InlineData(MigrationErrorAction.Terminate)]
    [InlineData(MigrationErrorAction.Ignore)]
    [InlineData(MigrationErrorAction.Undefined)]
    public void BuildStopChain_ActionThatRollsNothingBack_BuildsNoChain(MigrationErrorAction action)
    {
        // Arrange: the run migrated 01 and left 02 Failed under Ignore; 03 failed after 1 committed block
        var stopped = ThreeBlockFile("03_CreateTableB.sql", Release1);
        List<MigrationService.RunRecord> records =
            [Migrated(ThreeBlockFile("01_CreateTableA.sql", Release1), 1), LeftFailedUnderIgnore(ThreeBlockFile("02_Seed.sql", Release1), 2, 1)];

        // Act
        var chain = MigrationService.BuildStopChain(action, stopped, 3, MainDb, 1, records);

        // Assert
        chain.Should().BeNull(because: "the action rolls nothing back");
    }
}
