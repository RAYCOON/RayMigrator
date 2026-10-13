using AwesomeAssertions;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: OPT-2 tests for auto-fix orphaned runs behavior.
/// The core auto-fix logic (RepositoryMigrationRunInsertWithAutoFix) requires TemplateExecutor
/// and repository access, so it is tested via integration tests. The threshold it applies is
/// <c>Repository.OrphanedRunThresholdInMinutes</c> (see <see cref="MigrationRunHeartbeatTests"/>), measured
/// against the last heartbeat of the open run.
/// </summary>
public class AutoFixOrphanedRunTests
{
    [Fact]
    public void RepositoryMigrationRunInsertWithAutoFix_IsInternalMethod()
    {
        // Verify the method exists and is accessible for testing
        var method = typeof(MigrationService).GetMethod("RepositoryMigrationRunInsertWithAutoFix",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        method.Should().NotBeNull("RepositoryMigrationRunInsertWithAutoFix should be accessible");
        method!.ReturnType.Should().Be(typeof(System.Threading.Tasks.Task),
            "method should return Task for async execution");
    }

    [Fact]
    public void AutoFixThreshold_IsNoLongerAConstant()
    {
        typeof(MigrationService).GetField("AutoFixOrphanedRunsThresholdMinutes",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Should().BeNull("the threshold is Repository.OrphanedRunThresholdInMinutes since the heartbeat exists");
    }
}
