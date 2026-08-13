using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for FilterByTargetGroups and ValidateTargetGroupAliases.
/// These static methods control which target groups are included/excluded during execution.
/// Incorrect filtering could migrate the wrong target groups or miss required ones.
/// </summary>
public class FilterByTargetGroupTests
{
    private static List<MigrationFileInfo> CreateMultiTargetGroupFiles()
    {
        return new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Schema.sql", release: "Release 1.0", targetGroup: "Backend"),
            TestFactories.CreateMigrationFile(filename: "20_Data.sql", release: "Release 1.0", targetGroup: "Backend"),
            TestFactories.CreateMigrationFile(filename: "10_Styles.sql", release: "Release 1.0", targetGroup: "Frontend"),
            TestFactories.CreateMigrationFile(filename: "10_Report.sql", release: "Release 1.0", targetGroup: "Analytics"),
            TestFactories.CreateMigrationFile(filename: "10_AddCol.sql", release: "Release 1.1", targetGroup: "Backend"),
            TestFactories.CreateMigrationFile(filename: "10_NewView.sql", release: "Release 1.1", targetGroup: "Frontend"),
        };
    }

    private static List<TargetGroupOptions> CreateTargetGroupOptions()
    {
        return new List<TargetGroupOptions>
        {
            new() { Alias = "Backend" },
            new() { Alias = "Frontend" },
            new() { Alias = "Analytics" },
        };
    }

    // ──────────────────────────────────────────────────────
    // FilterByTargetGroups
    // ──────────────────────────────────────────────────────

    [Fact]
    public void FilterByTargetGroups_NullAliases_ReturnsAllFiles()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, null);

        result.Should().HaveCount(6);
        result.Should().BeSameAs(files);
    }

    [Fact]
    public void FilterByTargetGroups_EmptyArray_ReturnsAllFiles()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, Array.Empty<string>());

        result.Should().HaveCount(6);
        result.Should().BeSameAs(files);
    }

    [Fact]
    public void FilterByTargetGroups_SingleAlias_ReturnsOnlyMatchingFiles()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "Backend" });

        result.Should().HaveCount(3);
        result.Should().OnlyContain(f => f.TargetGroupAlias == "Backend");
    }

    [Fact]
    public void FilterByTargetGroups_SingleAlias_CaseInsensitive()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "backend" });

        result.Should().HaveCount(3);
        result.Should().OnlyContain(f => f.TargetGroupAlias == "Backend");
    }

    [Fact]
    public void FilterByTargetGroups_SingleAlias_NonExistentGroup_ReturnsEmpty()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "NonExistent" });

        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterByTargetGroups_MultipleAliases_ReturnsFilesFromAllSpecified()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "Backend", "Frontend" });

        result.Should().HaveCount(5);
        result.Select(f => f.TargetGroupAlias).Distinct()
            .Should().BeEquivalentTo(new[] { "Backend", "Frontend" });
    }

    [Fact]
    public void FilterByTargetGroups_MultipleAliases_ExcludesUnspecifiedGroups()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "Backend", "Frontend" });

        result.Should().NotContain(f => f.TargetGroupAlias == "Analytics");
    }

    [Fact]
    public void FilterByTargetGroups_MultipleAliases_CaseInsensitive()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "backend", "FRONTEND" });

        result.Should().HaveCount(5);
    }

    [Fact]
    public void FilterByTargetGroups_PreservesOrder()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "Backend" });

        result.Select(f => f.Filename).Should().ContainInOrder("10_Schema.sql", "20_Data.sql", "10_AddCol.sql");
    }

    [Fact]
    public void FilterByTargetGroups_DuplicateAliases_StillWorks()
    {
        var files = CreateMultiTargetGroupFiles();

        var result = MigrationService.FilterByTargetGroups(files, new[] { "Backend", "Backend" });

        result.Should().HaveCount(3);
        result.Should().OnlyContain(f => f.TargetGroupAlias == "Backend");
    }

    // ──────────────────────────────────────────────────────
    // ValidateTargetGroupAliases
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateTargetGroupAliases_NullAliases_DoesNotThrow()
    {
        var targetGroups = CreateTargetGroupOptions();

        var act = () => MigrationService.ValidateTargetGroupAliases(null, targetGroups);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateTargetGroupAliases_EmptyArray_DoesNotThrow()
    {
        var targetGroups = CreateTargetGroupOptions();

        var act = () => MigrationService.ValidateTargetGroupAliases(Array.Empty<string>(), targetGroups);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateTargetGroupAliases_SingleValidAlias_DoesNotThrow()
    {
        var targetGroups = CreateTargetGroupOptions();

        var act = () => MigrationService.ValidateTargetGroupAliases(new[] { "Backend" }, targetGroups);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateTargetGroupAliases_SingleValidAlias_CaseInsensitive()
    {
        var targetGroups = CreateTargetGroupOptions();

        var act = () => MigrationService.ValidateTargetGroupAliases(new[] { "backend" }, targetGroups);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateTargetGroupAliases_MultipleValidAliases_DoesNotThrow()
    {
        var targetGroups = CreateTargetGroupOptions();

        var act = () => MigrationService.ValidateTargetGroupAliases(new[] { "Backend", "Frontend" }, targetGroups);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateTargetGroupAliases_InvalidAlias_ThrowsWithAvailableGroups()
    {
        var targetGroups = CreateTargetGroupOptions();

        var act = () => MigrationService.ValidateTargetGroupAliases(new[] { "NonExistent" }, targetGroups);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'NonExistent'*not found*")
            .WithMessage("*'Backend'*'Frontend'*'Analytics'*");
    }
}
