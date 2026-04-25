
using FluentAssertions;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Unit tests for <see cref="RepositoryQueryHelper"/>.<c>ToSnakeCase</c>. The helper underpins
/// the DAL-017 PostgreSQL identifier-casing conversion used by <c>QuoteColumn</c>,
/// <c>GetQualifiedTableName</c>, <c>TableExists</c> and the PG CONCAT expression builder. The
/// critical invariant is that the product/brand token <c>RayMigrator</c> is treated as a single
/// word (-&gt; <c>raymigrator</c>) instead of being split into <c>ray_migrator</c>.
/// </summary>
public class RepositoryQueryHelperToSnakeCaseTests
{
    [Theory]
    // Mechanical mapping: lowercase-to-uppercase boundary becomes underscore + lowercase.
    [InlineData("MigrationRecord", "migration_record")]
    [InlineData("FileUpBlocksTotal", "file_up_blocks_total")]
    [InlineData("MigrationRunResultId", "migration_run_result_id")]
    [InlineData("TargetGroupAlias", "target_group_alias")]
    [InlineData("Id", "id")]
    [InlineData("NameLower", "name_lower")]
    // Already lowercase input stays unchanged.
    [InlineData("migration_record", "migration_record")]
    [InlineData("id", "id")]
    // Empty/whitespace passthrough.
    [InlineData("", "")]
    // Single uppercase character input.
    [InlineData("A", "a")]
    // RayMigrator product-name exception: stays as a single token.
    [InlineData("RayMigrator", "raymigrator")]
    [InlineData("RayMigratorVersion", "raymigrator_version")]
    [InlineData("RayMigratorHostMode", "raymigrator_host_mode")]
    [InlineData("CreatedByRayMigratorVersion", "created_by_raymigrator_version")]
    // RayMigrator exception followed by additional PascalCase tokens: the brand token stays merged,
    // additional words are split mechanically.
    [InlineData("RayMigratorWithSomethingElse", "raymigrator_with_something_else")]
    public void ToSnakeCase_ProducesExpectedOutput(string input, string expected)
    {
        var actual = RepositoryQueryHelper.ToSnakeCase(input);

        actual.Should().Be(expected);
    }
}
