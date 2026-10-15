using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: <see cref="MigrationRunResult.PartialSuccess"/> distinguishes a run that finished with skipped or
/// ignored files from an aborted one, <see cref="MigrationRunResult.Recovered"/> a failed run whose error-recovery
/// rollback completed cleanly (#18), <see cref="MigrationRunResult.Cancelled"/> a run the host stopped. The values are
/// seeded into the MigrationRunResult lookup table of every DAL, each by the schema step that introduced it, and must
/// never move.
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
    public void Cancelled_IsDefined_BetweenPartialSuccessAndRecovered()
    {
        Enum.IsDefined(MigrationRunResult.Cancelled).Should().BeTrue();
        ((byte)MigrationRunResult.Cancelled).Should().Be(70, "the value is seeded into the MigrationRunResult lookup table by schema step 3");
    }

    [Fact]
    public void EveryMember_IsSeededByTheSchemaStepThatIntroducedIt()
    {
        var root = FindRepoRoot();
        foreach (var dal in new[] { "SqlServer", "PostgreSQL", "Sqlite", "MySql", "MariaDb" })
        {
            foreach (var member in Enum.GetValues<MigrationRunResult>().Where(m => m != MigrationRunResult.Undefined))
            {
                // Cancelled came with step 3; a step never changes, so step 1 seeds the members it knew
                string step = member == MigrationRunResult.Cancelled ? "Repository_Upgrade_3.sql" : "Repository_Upgrade_1.sql";
                var template = File.ReadAllText(Path.Combine(root, $"Raycoon.RayMigrator.Database.{dal}", "Templates", step));

                // VALUES rows "(50, 'PartialSuccess'," (SqlServer, PostgreSQL, Sqlite) or the UNION ALL rows of the
                // MySql/MariaDb derived table ("SELECT 50, 'PartialSuccess'," / "SELECT 10 AS id, 'Running' AS name,").
                template.Should().MatchRegex($@"(\({(byte)member}, '{member}',|SELECT {(byte)member}( AS id)?, '{member}'( AS name)?,)",
                    $"{step} of {dal} must seed MigrationRunResult.{member}");
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
