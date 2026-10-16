using AwesomeAssertions;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P3-3: BaselineRequest model tests.
/// </summary>
public class BaselineRequestModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var request = new BaselineRequest();

        request.ProductAlias.Should().BeEmpty();
        request.Environment.Should().BeEmpty();
        request.TargetReleaseVersion.Should().BeNull();
        request.ShowInfo.Should().BeTrue();
        request.RevealSensitiveData.Should().BeFalse();
    }

    [Fact]
    public void TargetReleaseVersion_CanBeNull()
    {
        var request = new BaselineRequest
        {
            ProductAlias = "TestProduct",
            Environment = "Docker",
            TargetReleaseVersion = null
        };

        request.TargetReleaseVersion.Should().BeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var request = new BaselineRequest
        {
            ProductAlias = "TestProduct",
            Environment = "Docker",
            TargetReleaseVersion = "Release 1.0",
            ShowInfo = false,
            RevealSensitiveData = true
        };

        request.ProductAlias.Should().Be("TestProduct");
        request.Environment.Should().Be("Docker");
        request.TargetReleaseVersion.Should().Be("Release 1.0");
        request.ShowInfo.Should().BeFalse();
        request.RevealSensitiveData.Should().BeTrue();
    }
}

/// <summary>
/// P3-4: BaselineResult model tests.
/// </summary>
public class BaselineResultModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new BaselineResult();

        result.ProductAlias.Should().BeEmpty();
        result.TargetReleaseVersion.Should().BeNull();
        result.BaselinedFiles.Should().Be(0);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void TargetReleaseVersion_CanBeNull()
    {
        var result = new BaselineResult
        {
            Success = true,
            ProductAlias = "TestProduct",
            TargetReleaseVersion = null,
            BaselinedFiles = 10
        };

        result.TargetReleaseVersion.Should().BeNull();
        result.BaselinedFiles.Should().Be(10);
    }

    [Fact]
    public void InheritsFromOperationResult()
    {
        var result = new BaselineResult();

        result.Should().BeAssignableTo<OperationResult>();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var result = new BaselineResult
        {
            Success = true,
            ProductAlias = "TestProduct",
            TargetReleaseVersion = "Release 2.0",
            BaselinedFiles = 15,
            ErrorMessage = null,
            Messages = new List<string> { "Baseline completed" }
        };

        result.Success.Should().BeTrue();
        result.ProductAlias.Should().Be("TestProduct");
        result.TargetReleaseVersion.Should().Be("Release 2.0");
        result.BaselinedFiles.Should().Be(15);
        result.Messages.Should().HaveCount(1);
    }
}

/// <summary>
/// P3-5: MigrationStatus model tests (used by Info command).
/// </summary>
public class MigrationStatusModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var status = new MigrationStatusInfo();

        status.ProductAlias.Should().BeEmpty();
        status.CurrentRelease.Should().BeEmpty();
        status.LastMigrationDate.Should().BeNull();
        status.TotalMigrationsExecuted.Should().Be(0);
        status.PendingMigrations.Should().Be(0);
        status.PendingFiles.Should().BeEmpty();
        status.LastRunResult.Should().BeNull();
        status.TargetGroups.Should().BeEmpty();
    }

    [Fact]
    public void TargetGroupStatus_DefaultValues_AreCorrect()
    {
        var tg = new TargetGroupStatus();

        tg.Alias.Should().BeEmpty();
        tg.DatabaseType.Should().BeEmpty();
        tg.CurrentRelease.Should().BeEmpty();
        tg.ExecutedMigrations.Should().Be(0);
        tg.PendingMigrations.Should().Be(0);
        tg.LastMigrationDate.Should().BeNull();
        tg.Targets.Should().BeEmpty();
    }
}

/// <summary>
/// P3-7: PendingMigrationInfo and PendingMigrationReason model tests (used by the Info command). The reason follows the
/// enum convention of the engine: Undefined = 0 and fixed values in steps of 10 that hosts may store.
/// </summary>
public class PendingMigrationModelTests
{
    [Fact]
    public void PendingMigrationReason_Values_FollowTheEnumConvention()
    {
        Enum.GetValues<PendingMigrationReason>().Should().Equal(
            PendingMigrationReason.Undefined, PendingMigrationReason.NotMigrated, PendingMigrationReason.Failed,
            PendingMigrationReason.Resumable, PendingMigrationReason.RunAlways, PendingMigrationReason.HashMismatch,
            PendingMigrationReason.RollbackIncomplete);
        Enum.GetValues<PendingMigrationReason>().Select(r => (int)r).Should().Equal([0, 10, 20, 30, 40, 50, 60],
            because: "the values are part of the contract and never move");
        default(PendingMigrationReason).Should().Be(PendingMigrationReason.Undefined);
    }

    [Fact]
    public void PendingMigrationInfo_AllProperties_AreSet()
    {
        var pending = new PendingMigrationInfo("Release 1.0", "Backend", "MainDB", "10_Create.sql", 4, PendingMigrationReason.Resumable, 2);

        pending.ReleaseVersion.Should().Be("Release 1.0");
        pending.TargetGroupAlias.Should().Be("Backend");
        pending.TargetAlias.Should().Be("MainDB");
        pending.Filename.Should().Be("10_Create.sql");
        pending.FileOrderId.Should().Be(4);
        pending.Reason.Should().Be(PendingMigrationReason.Resumable);
        pending.ResumeFromBlock.Should().Be(2);
        pending.RollbackBlocksMigrated.Should().BeNull(because: "only an incomplete rollback has rollback blocks");
        pending.RollbackBlocksTotal.Should().BeNull();
    }

    [Fact]
    public void PendingMigrationInfo_RollbackIncomplete_CarriesTheRollbackBlocks()
    {
        var pending = new PendingMigrationInfo("Release 1.0", "Backend", "MainDB", "10_Create.sql", 4, PendingMigrationReason.RollbackIncomplete, null, 1, 3);

        pending.RollbackBlocksMigrated.Should().Be(1);
        pending.RollbackBlocksTotal.Should().Be(3);
        pending.ResumeFromBlock.Should().BeNull();
    }

    [Fact]
    public void PendingMigrationInfo_IsComparedByValue()
    {
        var pending = new PendingMigrationInfo("Release 1.0", "Backend", null, "10_Create.sql", 4, PendingMigrationReason.NotMigrated, null);

        pending.Should().Be(new PendingMigrationInfo("Release 1.0", "Backend", null, "10_Create.sql", 4, PendingMigrationReason.NotMigrated, null));
        pending.Should().NotBe(pending with { TargetAlias = "MainDB" });
    }
}

/// <summary>
/// P3-6: MigrationHistory model tests (used by Info command).
/// </summary>
public class MigrationHistoryModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var history = new MigrationHistory();

        history.ProductAlias.Should().BeEmpty();
        history.Runs.Should().BeEmpty();
    }

    [Fact]
    public void MigrationRunInfo_DefaultValues_AreCorrect()
    {
        var run = new MigrationRunInfo();

        run.MigrationRunId.Should().Be(0);
        run.Environment.Should().BeEmpty();
        run.TotalMigrations.Should().Be(0);
        run.SuccessfulMigrations.Should().Be(0);
        run.FailedMigrations.Should().Be(0);
        run.FromRelease.Should().BeNull();
        run.CompletedAt.Should().BeNull();
        run.ToRelease.Should().BeNull();
    }
}
