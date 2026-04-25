using FluentAssertions;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: OPT-2 tests for auto-fix orphaned runs behavior.
/// The core auto-fix logic (RepositoryMigrationRunInsertWithAutoFix) requires TemplateExecutor
/// and repository access, so it is tested via integration tests. These unit tests verify
/// the configurable threshold and method accessibility.
/// </summary>
public class AutoFixOrphanedRunTests
{
    [Fact]
    public void AutoFixThreshold_IsReasonableDefault()
    {
        // The threshold should be long enough to not interfere with genuinely running migrations
        // but short enough to be useful for auto-recovery after crashes.
        MigrationService.AutoFixOrphanedRunsThresholdMinutes.Should().BeGreaterThanOrEqualTo(5,
            "threshold must be high enough to avoid interfering with legitimately running migrations");
        MigrationService.AutoFixOrphanedRunsThresholdMinutes.Should().BeLessThanOrEqualTo(60,
            "threshold should not be so high that it defeats the purpose of auto-recovery");
    }

    [Fact]
    public void AutoFixThreshold_DefaultIsTenMinutes()
    {
        MigrationService.AutoFixOrphanedRunsThresholdMinutes.Should().Be(10);
    }

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
}
