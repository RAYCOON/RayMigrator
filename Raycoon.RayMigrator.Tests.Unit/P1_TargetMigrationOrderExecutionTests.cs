using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-4: TargetMigrationOrder execution order tests.
/// GetExecutionOrder determines the iteration sequence of (file, target) pairs based on TargetMigrationOrder.
/// Incorrect ordering would cause migrations to execute in the wrong sequence, potentially breaking
/// database schema dependencies or causing data corruption.
/// </summary>
public class TargetMigrationOrderExecutionTests
{
    private static MigrationFileInfo CreateFile(
        int fileOrderId, string release = "Release 1.0", string targetGroup = "Backend")
    {
        return new MigrationFileInfo
        {
            Filename = $"{fileOrderId:D2}_Migration.sql",
            ReleaseVersion = release,
            TargetGroupAlias = targetGroup,
            FileOrderId = fileOrderId,
            FileUpHash = $"hash_{fileOrderId}"
        };
    }

    private static TargetGroupOptions CreateTargetGroup(
        string targetMigrationOrder, string[] targetAliases, string alias = "Backend")
    {
        return new TargetGroupOptions
        {
            Alias = alias,
            DatabaseType = "SqlServer",
            TargetMigrationOrder = targetMigrationOrder,
            Targets = targetAliases.Select(a => new TargetOptions { Alias = a, ConnectionString = $"Server={a}" }).ToList()
        };
    }

    #region Simultaneously

    [Fact]
    public void Simultaneously_IterationOrder_IsFileThenTarget()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1), CreateFile(2) };
        var targetGroup = CreateTargetGroup("Simultaneously", ["T1", "T2"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert
        order.Should().HaveCount(4);
        order[0].Should().Be((1, "T1")); // File 1 → Target 1
        order[1].Should().Be((1, "T2")); // File 1 → Target 2
        order[2].Should().Be((2, "T1")); // File 2 → Target 1
        order[3].Should().Be((2, "T2")); // File 2 → Target 2
    }

    [Fact]
    public void Simultaneously_ThreeFilesThreeTargets_CorrectOrder()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1), CreateFile(2), CreateFile(3) };
        var targetGroup = CreateTargetGroup("Simultaneously", ["T1", "T2", "T3"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert
        order.Should().HaveCount(9);
        // File 1 on all targets first
        order[0].Should().Be((1, "T1"));
        order[1].Should().Be((1, "T2"));
        order[2].Should().Be((1, "T3"));
        // File 2 on all targets
        order[3].Should().Be((2, "T1"));
        order[4].Should().Be((2, "T2"));
        order[5].Should().Be((2, "T3"));
        // File 3 on all targets
        order[6].Should().Be((3, "T1"));
        order[7].Should().Be((3, "T2"));
        order[8].Should().Be((3, "T3"));
    }

    #endregion Simultaneously

    #region Successively

    [Fact]
    public void Successively_IterationOrder_IsTargetThenFile()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1), CreateFile(2) };
        var targetGroup = CreateTargetGroup("Successively", ["T1", "T2"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert
        order.Should().HaveCount(4);
        order[0].Should().Be((1, "T1")); // Target 1 → File 1
        order[1].Should().Be((2, "T1")); // Target 1 → File 2
        order[2].Should().Be((1, "T2")); // Target 2 → File 1
        order[3].Should().Be((2, "T2")); // Target 2 → File 2
    }

    [Fact]
    public void Successively_ThreeFilesThreeTargets_CorrectOrder()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1), CreateFile(2), CreateFile(3) };
        var targetGroup = CreateTargetGroup("Successively", ["T1", "T2", "T3"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert
        order.Should().HaveCount(9);
        // All files on Target 1 first
        order[0].Should().Be((1, "T1"));
        order[1].Should().Be((2, "T1"));
        order[2].Should().Be((3, "T1"));
        // All files on Target 2
        order[3].Should().Be((1, "T2"));
        order[4].Should().Be((2, "T2"));
        order[5].Should().Be((3, "T2"));
        // All files on Target 3
        order[6].Should().Be((1, "T3"));
        order[7].Should().Be((2, "T3"));
        order[8].Should().Be((3, "T3"));
    }

    #endregion Successively

    #region Undefined / Defaults

    [Fact]
    public void UndefinedTargetMigrationOrder_DefaultsToSuccessively()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1), CreateFile(2) };
        var targetGroup = CreateTargetGroup("Undefined", ["T1", "T2"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert — same as Successively (target → file)
        order.Should().HaveCount(4);
        order[0].Should().Be((1, "T1"));
        order[1].Should().Be((2, "T1"));
        order[2].Should().Be((1, "T2"));
        order[3].Should().Be((2, "T2"));
    }

    [Fact]
    public void NullTargetMigrationOrder_DefaultsToSuccessively()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1), CreateFile(2) };
        var targetGroup = new TargetGroupOptions
        {
            Alias = "Backend",
            DatabaseType = "SqlServer",
            TargetMigrationOrder = null, // not set
            Targets = new List<TargetOptions>
            {
                new() { Alias = "T1", ConnectionString = "Server=T1" },
                new() { Alias = "T2", ConnectionString = "Server=T2" }
            }
        };

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert — defaults to Successively (target → file)
        order[0].Should().Be((1, "T1"));
        order[1].Should().Be((2, "T1"));
        order[2].Should().Be((1, "T2"));
        order[3].Should().Be((2, "T2"));
    }

    #endregion Undefined / Defaults

    #region Single Target

    [Fact]
    public void SingleTarget_BothModesProduceSameOrder()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1), CreateFile(2), CreateFile(3) };
        var simultaneously = CreateTargetGroup("Simultaneously", ["T1"]);
        var successively = CreateTargetGroup("Successively", ["T1"]);

        // Act
        var orderSimul = MigrationService.GetExecutionOrder(files, simultaneously);
        var orderSucc = MigrationService.GetExecutionOrder(files, successively);

        // Assert
        orderSimul.Should().BeEquivalentTo(orderSucc, options => options.WithStrictOrdering());
        orderSimul.Should().HaveCount(3);
        orderSimul[0].Should().Be((1, "T1"));
        orderSimul[1].Should().Be((2, "T1"));
        orderSimul[2].Should().Be((3, "T1"));
    }

    #endregion Single Target

    #region Edge Cases

    [Fact]
    public void EmptyFiles_ReturnsEmptyList()
    {
        // Arrange
        var files = new List<MigrationFileInfo>();
        var targetGroup = CreateTargetGroup("Simultaneously", ["T1", "T2"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert
        order.Should().BeEmpty();
    }

    [Fact]
    public void EmptyTargets_ReturnsEmptyList()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1) };
        var targetGroup = new TargetGroupOptions
        {
            Alias = "Backend",
            DatabaseType = "SqlServer",
            TargetMigrationOrder = "Simultaneously",
            Targets = new List<TargetOptions>()
        };

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert
        order.Should().BeEmpty();
    }

    [Fact]
    public void SingleFile_SingleTarget_ReturnsSingleEntry()
    {
        // Arrange
        var files = new List<MigrationFileInfo> { CreateFile(1) };
        var targetGroup = CreateTargetGroup("Simultaneously", ["T1"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert
        order.Should().ContainSingle().Which.Should().Be((1, "T1"));
    }

    [Fact]
    public void FileOrderPreserved_SimultaneouslyMode()
    {
        // Arrange — files with non-sequential order IDs
        var files = new List<MigrationFileInfo> { CreateFile(5), CreateFile(10), CreateFile(15) };
        var targetGroup = CreateTargetGroup("Simultaneously", ["T1"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert — preserves input order
        order.Select(o => o.FileOrderId).Should().ContainInOrder(5, 10, 15);
    }

    [Fact]
    public void FileOrderPreserved_SuccessivelyMode()
    {
        // Arrange — files with non-sequential order IDs
        var files = new List<MigrationFileInfo> { CreateFile(5), CreateFile(10), CreateFile(15) };
        var targetGroup = CreateTargetGroup("Successively", ["T1", "T2"]);

        // Act
        var order = MigrationService.GetExecutionOrder(files, targetGroup);

        // Assert — files maintain their relative order within each target block
        var t1Files = order.Where(o => o.TargetAlias == "T1").Select(o => o.FileOrderId).ToList();
        var t2Files = order.Where(o => o.TargetAlias == "T2").Select(o => o.FileOrderId).ToList();
        t1Files.Should().ContainInOrder(5, 10, 15);
        t2Files.Should().ContainInOrder(5, 10, 15);
    }

    #endregion Edge Cases

    #region TargetGroupExecutionResult

    [Fact]
    public void TargetGroupExecutionResult_DefaultState_IsSuccess()
    {
        var result = new MigrationService.TargetGroupExecutionResult();

        result.Success.Should().BeTrue();
        result.SuccessCount.Should().Be(0);
        result.FailCount.Should().Be(0);
        result.FailedFile.Should().BeNull();
        result.FailedMigrationRecordId.Should().Be(0);
        result.ErrorMessage.Should().BeNull();
    }

    #endregion TargetGroupExecutionResult

    #region GetFullExecutionOrder — Release-based ordering

    [Fact]
    public void FullOrder_Simultaneously_MultiRelease_ReleaseByRelease()
    {
        // Arrange: 2 releases, 2 target groups, Simultaneously mode
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.0", "Frontend"),
            CreateFile(3, "Release 1.1", "Backend"),
            CreateFile(4, "Release 1.1", "Frontend"),
        };

        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Simultaneously", ["T1", "T2"], "Backend"),
            CreateTargetGroup("Simultaneously", ["T1", "T2"], "Frontend"),
        };

        // Act
        var order = MigrationService.GetFullExecutionOrder(files, targetGroups);

        // Assert: R1.0/Backend → R1.0/Frontend → R1.1/Backend → R1.1/Frontend
        order.Should().HaveCount(8);
        // R1.0 Backend: file 1 → T1, T2
        order[0].Should().Be((1, "Backend", "T1"));
        order[1].Should().Be((1, "Backend", "T2"));
        // R1.0 Frontend: file 2 → T1, T2
        order[2].Should().Be((2, "Frontend", "T1"));
        order[3].Should().Be((2, "Frontend", "T2"));
        // R1.1 Backend: file 3 → T1, T2
        order[4].Should().Be((3, "Backend", "T1"));
        order[5].Should().Be((3, "Backend", "T2"));
        // R1.1 Frontend: file 4 → T1, T2
        order[6].Should().Be((4, "Frontend", "T1"));
        order[7].Should().Be((4, "Frontend", "T2"));
    }

    [Fact]
    public void FullOrder_Successively_MultiRelease_ReleaseByRelease()
    {
        // Arrange: 2 releases, 2 target groups, Successively mode
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.0", "Frontend"),
            CreateFile(3, "Release 1.1", "Backend"),
            CreateFile(4, "Release 1.1", "Frontend"),
        };

        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Successively", ["T1", "T2"], "Backend"),
            CreateTargetGroup("Successively", ["T1", "T2"], "Frontend"),
        };

        // Act
        var order = MigrationService.GetFullExecutionOrder(files, targetGroups);

        // Assert: R1.0/Backend(target→file) → R1.0/Frontend(target→file) → R1.1/...
        order.Should().HaveCount(8);
        // R1.0 Backend: T1→file1, T2→file1
        order[0].Should().Be((1, "Backend", "T1"));
        order[1].Should().Be((1, "Backend", "T2"));
        // R1.0 Frontend: T1→file2, T2→file2
        order[2].Should().Be((2, "Frontend", "T1"));
        order[3].Should().Be((2, "Frontend", "T2"));
        // R1.1 Backend: T1→file3, T2→file3
        order[4].Should().Be((3, "Backend", "T1"));
        order[5].Should().Be((3, "Backend", "T2"));
        // R1.1 Frontend: T1→file4, T2→file4
        order[6].Should().Be((4, "Frontend", "T1"));
        order[7].Should().Be((4, "Frontend", "T2"));
    }

    [Fact]
    public void FullOrder_MixedModes_RespectsPerTargetGroupMode()
    {
        // Arrange: Backend=Simultaneously, Frontend=Successively, 2 files per TG per release
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.0", "Backend"),
            CreateFile(3, "Release 1.0", "Frontend"),
            CreateFile(4, "Release 1.0", "Frontend"),
        };

        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Simultaneously", ["T1", "T2"], "Backend"),
            CreateTargetGroup("Successively", ["T1", "T2"], "Frontend"),
        };

        // Act
        var order = MigrationService.GetFullExecutionOrder(files, targetGroups);

        // Assert
        order.Should().HaveCount(8);
        // Backend Simultaneously: file→target (file1→T1,T2, file2→T1,T2)
        order[0].Should().Be((1, "Backend", "T1"));
        order[1].Should().Be((1, "Backend", "T2"));
        order[2].Should().Be((2, "Backend", "T1"));
        order[3].Should().Be((2, "Backend", "T2"));
        // Frontend Successively: target→file (T1→file3,file4, T2→file3,file4)
        order[4].Should().Be((3, "Frontend", "T1"));
        order[5].Should().Be((4, "Frontend", "T1"));
        order[6].Should().Be((3, "Frontend", "T2"));
        order[7].Should().Be((4, "Frontend", "T2"));
    }

    [Fact]
    public void FullOrder_SingleRelease_SameAsBefore()
    {
        // Arrange: Single release — same behavior as before
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.0", "Backend"),
        };

        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Simultaneously", ["T1", "T2"], "Backend"),
        };

        // Act
        var order = MigrationService.GetFullExecutionOrder(files, targetGroups);

        // Assert — identical to GetExecutionOrder for single TG, single release
        order.Should().HaveCount(4);
        order[0].Should().Be((1, "Backend", "T1"));
        order[1].Should().Be((1, "Backend", "T2"));
        order[2].Should().Be((2, "Backend", "T1"));
        order[3].Should().Be((2, "Backend", "T2"));
    }

    [Fact]
    public void FullOrder_ThreeReleases_StrictOrder()
    {
        // Arrange: 3 releases, single TG
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.1", "Backend"),
            CreateFile(3, "Release 1.2", "Backend"),
        };

        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Simultaneously", ["T1"], "Backend"),
        };

        // Act
        var order = MigrationService.GetFullExecutionOrder(files, targetGroups);

        // Assert: strictly sequential releases
        order.Should().HaveCount(3);
        order[0].Should().Be((1, "Backend", "T1"));
        order[1].Should().Be((2, "Backend", "T1"));
        order[2].Should().Be((3, "Backend", "T1"));
    }

    [Fact]
    public void FullOrder_EmptyReleaseSkipped()
    {
        // Arrange: R1.0 has Backend, R1.1 has no Backend files (only Frontend)
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.1", "Frontend"),
        };

        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Simultaneously", ["T1"], "Backend"),
            CreateTargetGroup("Simultaneously", ["T1"], "Frontend"),
        };

        // Act
        var order = MigrationService.GetFullExecutionOrder(files, targetGroups);

        // Assert: R1.0/Backend → R1.1/Frontend (R1.0/Frontend and R1.1/Backend skipped)
        order.Should().HaveCount(2);
        order[0].Should().Be((1, "Backend", "T1"));
        order[1].Should().Be((2, "Frontend", "T1"));
    }

    [Fact]
    public void FullOrder_TargetGroupConfigOrder_Respected()
    {
        // Arrange: Config order is Frontend first, then Backend
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.0", "Frontend"),
        };

        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Simultaneously", ["T1"], "Frontend"),  // Frontend FIRST in config
            CreateTargetGroup("Simultaneously", ["T1"], "Backend"),   // Backend second
        };

        // Act
        var order = MigrationService.GetFullExecutionOrder(files, targetGroups);

        // Assert: Frontend before Backend (config order, not alphabetical)
        order.Should().HaveCount(2);
        order[0].Should().Be((2, "Frontend", "T1"));
        order[1].Should().Be((1, "Backend", "T1"));
    }

    #endregion GetFullExecutionOrder — Release-based ordering
}
