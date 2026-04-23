// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for FilterByTargetRelease and FilterReleasesAfterTarget.
/// These static methods control which releases are included/excluded when migrating up/down.
/// Incorrect filtering could migrate too many or too few releases.
/// </summary>
public class FilterByTargetReleaseTests
{
    private static List<MigrationFileInfo> CreateMultiReleaseFiles()
    {
        return new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Schema.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationFile(filename: "20_Data.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationFile(filename: "10_AddColumn.sql", release: "Release 1.1"),
            TestFactories.CreateMigrationFile(filename: "10_NewTable.sql", release: "Release 1.2"),
            TestFactories.CreateMigrationFile(filename: "10_Cleanup.sql", release: "Release 1.3"),
        };
    }

    // ──────────────────────────────────────────────────────
    // FilterByTargetRelease (Migrate-Up / Baseline: <= target)
    // ──────────────────────────────────────────────────────

    [Fact]
    public void FilterByTargetRelease_NullTarget_ReturnsAllFiles()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, null);

        result.Should().HaveCount(5);
        result.Should().BeSameAs(files);
    }

    [Fact]
    public void FilterByTargetRelease_EmptyTarget_ReturnsAllFiles()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "");

        result.Should().HaveCount(5);
        result.Should().BeSameAs(files);
    }

    [Fact]
    public void FilterByTargetRelease_WhitespaceTarget_ReturnsAllFiles()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "   ");

        result.Should().HaveCount(5);
        result.Should().BeSameAs(files);
    }

    [Fact]
    public void FilterByTargetRelease_ExactMatch_IncludesMatchingRelease()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "Release 1.1");

        result.Should().HaveCount(3);
        result.Select(f => f.ReleaseVersion).Distinct()
            .Should().BeEquivalentTo(new[] { "Release 1.0", "Release 1.1" });
    }

    [Fact]
    public void FilterByTargetRelease_FiltersOutLaterReleases()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "Release 1.1");

        result.Should().NotContain(f => f.ReleaseVersion == "Release 1.2");
        result.Should().NotContain(f => f.ReleaseVersion == "Release 1.3");
    }

    [Fact]
    public void FilterByTargetRelease_IncludesAllEarlierReleases()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "Release 1.1");

        result.Where(f => f.ReleaseVersion == "Release 1.0").Should().HaveCount(2);
        result.Where(f => f.ReleaseVersion == "Release 1.1").Should().HaveCount(1);
    }

    [Fact]
    public void FilterByTargetRelease_CaseInsensitive()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "release 1.1");

        result.Should().HaveCount(3);
    }

    [Fact]
    public void FilterByTargetRelease_NonExistentLowTarget_ReturnsEmpty()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "Release 0.5");

        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterByTargetRelease_HighTarget_IncludesAll()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterByTargetRelease(files, "Release 99.0");

        result.Should().HaveCount(5);
    }

    // ──────────────────────────────────────────────────────
    // FilterReleasesAfterTarget (Migrate-Down: > target)
    // ──────────────────────────────────────────────────────

    [Fact]
    public void FilterReleasesAfterTarget_ReturnsOnlyReleasesAfterTarget()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterReleasesAfterTarget(files, "Release 1.1");

        result.Should().HaveCount(2);
        result.Select(f => f.ReleaseVersion).Distinct()
            .Should().BeEquivalentTo(new[] { "Release 1.2", "Release 1.3" });
    }

    [Fact]
    public void FilterReleasesAfterTarget_ExactMatchRelease_IsExcluded()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterReleasesAfterTarget(files, "Release 1.1");

        result.Should().NotContain(f => f.ReleaseVersion == "Release 1.1");
    }

    [Fact]
    public void FilterReleasesAfterTarget_HighTarget_ReturnsEmpty()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterReleasesAfterTarget(files, "Release 99.0");

        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterReleasesAfterTarget_LowTarget_ReturnsAll()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterReleasesAfterTarget(files, "Release 0.0");

        result.Should().HaveCount(5);
    }

    [Fact]
    public void FilterReleasesAfterTarget_CaseInsensitive()
    {
        var files = CreateMultiReleaseFiles();

        var result = MigrationService.FilterReleasesAfterTarget(files, "release 1.1");

        result.Should().HaveCount(2);
    }
}
