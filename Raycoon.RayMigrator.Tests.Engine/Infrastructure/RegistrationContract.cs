using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Templates;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The registration of a product and of an environment under first starts at the same moment: hosts that start the
/// first run of a new product or environment at once register its name once, and every start gets the Id of its row.
/// <c>Repository_Product_CheckInsert</c> and <c>Repository_Environment_CheckInsert</c> look the name up and insert it
/// when it is missing; a start that found it missing as well tolerates the row of the other start instead of failing
/// on the unique index of <c>NameLower</c>: SQL Server locks the name in a transaction (<c>UPDLOCK, HOLDLOCK</c>),
/// PostgreSQL inserts with <c>ON CONFLICT (name_lower) DO NOTHING</c>, MySQL and MariaDB with
/// <c>ON DUPLICATE KEY UPDATE</c>, SQLite with <c>ON CONFLICT ("NameLower") DO NOTHING</c>, and each reads the Id of the
/// row. The starts run the template of the engine on DALs of their own, as the processes of several hosts do.
/// </summary>
internal static class RegistrationContract
{
    /// <summary>How many hosts register the same new name at the same moment in each round.</summary>
    private const int ParallelStarts = 8;

    /// <summary>How many rounds of parallel first registrations run, each with a name of its own.</summary>
    private const int Rounds = 5;

    /// <summary>
    /// After a migrate-up created the repository, each round lets <see cref="ParallelStarts"/> starts of their own, each
    /// on a DAL and a connection of its own, run <c>Repository_Product_CheckInsert</c> at once with a product name that
    /// is new in the round: no start fails, all get the same Id, which a later start finds as well, and the round adds
    /// exactly one Product row.
    /// </summary>
    public static Task VerifyParallelFirstRegistrationsCreateTheProductOnceAsync(ScenarioBuilder builder)
        => VerifyParallelFirstRegistrationsAsync(builder, TemplateType.Repository_Product_CheckInsert, "Product");

    /// <summary>
    /// The same as <see cref="VerifyParallelFirstRegistrationsCreateTheProductOnceAsync"/> for an environment name and
    /// <c>Repository_Environment_CheckInsert</c>.
    /// </summary>
    public static Task VerifyParallelFirstRegistrationsCreateTheEnvironmentOnceAsync(ScenarioBuilder builder)
        => VerifyParallelFirstRegistrationsAsync(builder, TemplateType.Repository_Environment_CheckInsert, "Environment");

    private static async Task VerifyParallelFirstRegistrationsAsync(ScenarioBuilder builder, TemplateType templateType, string table)
    {
        // Arrange: the migrate-up created the repository and registered the product and the environment of the scenario
        await using var ctx = await builder.BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        int rows = ctx.CountRepoRows(table);
        var failedStarts = new List<string>();
        var findings = new List<string>();

        // Act
        for (int round = 1; round <= Rounds; round++)
        {
            string name = $"{table}Race{round}_{Guid.NewGuid():N}";
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = Enumerable.Range(0, ParallelStarts).Select(_ => ctx.RegisterWithNewDalAsync(templateType, name, start.Task)).ToList();
            start.SetResult();

            var ids = new List<int>();
            foreach (var task in starts)
            {
                try
                {
                    ids.Add(await task);
                }
                catch (Exception ex)
                {
                    var cause = ex.GetBaseException();
                    failedStarts.Add($"round {round}: {cause.GetType().Name}: {cause.Message}");
                }
            }

            // A start after the round finds the row of the name and returns its Id
            int found = await ctx.RegisterWithNewDalAsync(templateType, name, Task.CompletedTask);
            int rowsAfter = ctx.CountRepoRows(table);
            var distinct = ids.Distinct().ToList();
            if (distinct.Count != 1 || distinct[0] != found || rowsAfter != rows + 1)
                findings.Add($"round {round}: the starts got the Ids [{string.Join(", ", ids)}], a later start found [{found}], {rowsAfter - rows} new row(s)");
            rows = rowsAfter;
        }

        // Assert
        failedStarts.Should().BeEmpty(because: "every first start registers the {0} or finds it, but {1} of {2} parallel first registrations failed",
            table, failedStarts.Count, Rounds * ParallelStarts);
        findings.Should().BeEmpty(because: "in every round all {0} starts get the Id of the one new {1} row, but {2} of {3} rounds broke it ({4})",
            ParallelStarts, table, findings.Count, Rounds, string.Join("; ", findings));
    }
}
