using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="MigrationService.GetHighestMigratedRelease"/> is the one definition of the "current release"
/// of a product and environment: Info reports it and every run stamps it into MigrationRun.FromReleaseVersion.
/// </summary>
public class HighestMigratedReleaseTests
{
    private static MigrationRecord Record(string release, MigrationStatus status)
        => new() { ReleaseVersion = release, MigrationStatusId = status, Filename = "001.sql" };

    [Fact]
    public void GetHighestMigratedRelease_IgnoresRecordsThatAreNotMigrated()
    {
        var records = new[]
        {
            Record("Release_1.0", MigrationStatus.Migrated),
            Record("Release_2.0", MigrationStatus.Migrated),
            Record("Release_3.0", MigrationStatus.Failed),
            Record("Release_4.0", MigrationStatus.NotMigrated),
        };

        MigrationService.GetHighestMigratedRelease(records).Should().Be("Release_2.0");
    }

    [Fact]
    public void GetHighestMigratedRelease_ComparesOrdinalIgnoringCase()
    {
        var records = new[]
        {
            Record("release_1.0", MigrationStatus.Migrated),
            Record("Release_1.1", MigrationStatus.Migrated),
        };

        MigrationService.GetHighestMigratedRelease(records).Should().Be("Release_1.1");
    }

    [Fact]
    public void GetHighestMigratedRelease_NothingMigrated_ReturnsNull()
    {
        MigrationService.GetHighestMigratedRelease([]).Should().BeNull();
        MigrationService.GetHighestMigratedRelease([Record("Release_1.0", MigrationStatus.Failed)]).Should().BeNull();
    }
}
