using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Xunit;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// Ensures every MigrationCommand enum value is explicitly listed in the engine pipeline command set.
/// If a new value is added to MigrationCommand without updating this set, the test fails —
/// forcing the developer to decide how the Engine CLI should handle it.
/// </summary>
public class MigrationCommandExhaustivenessTests
{
    /// <summary>
    /// Commands processed by Engine CLI through environment resolution / pipeline.
    /// Update this set when adding new commands that the engine handles directly.
    /// </summary>
    private static readonly HashSet<MigrationCommand> EnginePipelineCommands =
    [
        MigrationCommand.None,
        MigrationCommand.MigrateUp,
        MigrationCommand.MigrateDown,
        MigrationCommand.ValidateHash,
        MigrationCommand.UpdateHash,
        MigrationCommand.Info,
        MigrationCommand.Baseline,
        MigrationCommand.FixIssues,
    ];

    [Fact]
    public void AllMigrationCommands_AreHandled_InEngineConsole()
    {
        var allCommands = Enum.GetValues<MigrationCommand>();

        foreach (var command in allCommands)
        {
            EnginePipelineCommands.Should().Contain(command,
                $"MigrationCommand.{command} was added but not categorized in Engine Console dispatch. " +
                $"Add it to {nameof(EnginePipelineCommands)} and update Program.cs accordingly.");
        }
    }
}
