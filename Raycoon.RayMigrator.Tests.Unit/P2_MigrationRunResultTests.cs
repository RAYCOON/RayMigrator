using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: <see cref="MigrationRunResult.PartialSuccess"/> distinguishes a run that finished with skipped or
/// ignored files from an aborted one, <see cref="MigrationRunResult.Recovered"/> a failed run whose error-recovery
/// rollback completed cleanly (#18). The values are seeded into the MigrationRunResult lookup table of every DAL
/// and must never move.
/// </summary>
public class MigrationRunResultTests
{
    [Fact]
    public void PartialSuccess_IsDefined_BetweenRunningAndError()
    {
        Enum.IsDefined(MigrationRunResult.PartialSuccess).Should().BeTrue();
        ((byte)MigrationRunResult.PartialSuccess).Should().Be(50, "the value is seeded into the MigrationRunResult lookup table of every DAL");
        ((byte)MigrationRunResult.Running).Should().Be(10);
        ((byte)MigrationRunResult.Error).Should().Be(90);
        ((byte)MigrationRunResult.Ok).Should().Be(100);
    }

    [Fact]
    public void Recovered_IsDefined_BetweenPartialSuccessAndError()
    {
        Enum.IsDefined(MigrationRunResult.Recovered).Should().BeTrue();
        ((byte)MigrationRunResult.Recovered).Should().Be(80, "the value is seeded into the MigrationRunResult lookup table of every DAL");
    }

    [Fact]
    public void EveryMember_IsSeededByEveryRepositoryCheckCreateTemplate()
    {
        var root = FindRepoRoot();
        foreach (var dal in new[] { "SqlServer", "PostgreSQL", "Sqlite", "MySql", "MariaDb" })
        {
            var template = File.ReadAllText(Path.Combine(root, $"Raycoon.RayMigrator.Database.{dal}", "Templates", "Repository_CheckCreate.sql"));
            foreach (var member in Enum.GetValues<MigrationRunResult>().Where(m => m != MigrationRunResult.Undefined))
            {
                // VALUES rows "(50, 'PartialSuccess'," (SqlServer, PostgreSQL, Sqlite) or the UNION ALL rows of the
                // MySql/MariaDb derived table ("SELECT 50, 'PartialSuccess'," / "SELECT 10 AS id, 'Running' AS name,").
                template.Should().MatchRegex($@"(\({(byte)member}, '{member}',|SELECT {(byte)member}( AS id)?, '{member}'( AS name)?,)",
                    $"Repository_CheckCreate.sql of {dal} must seed MigrationRunResult.{member}");
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RayMigrator.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the test must run from within the repository");
        return dir!.FullName;
    }
}
