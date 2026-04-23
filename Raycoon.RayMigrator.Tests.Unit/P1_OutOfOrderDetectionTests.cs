// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-3: Out-of-Order migration detection tests.
/// DetectOutOfOrderFiles identifies pending files from releases older than the highest already-migrated release.
/// Errors here could silently skip migrations or incorrectly block valid migrations.
/// </summary>
public class DetectOutOfOrderFilesTests
{
    [Fact]
    public void NoExistingRecords_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile() };
        var records = new List<MigrationRecord>();

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void NoFilesToMigrate_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>();
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord() };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllFilesFromNewerRelease_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 2.0"),
            TestFactories.CreateMigrationFile(filename: "20_B.sql", release: "Release 2.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllFilesFromSameRelease_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "20_New.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 1.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FileFromOlderRelease_IsDetected()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().HaveCount(1);
        result[0].Filename.Should().Be("10_Missed.sql");
    }

    [Fact]
    public void MixedReleasesFiles_OnlyOlderDetected()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Old.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationFile(filename: "20_New.sql", release: "Release 2.0"),
            TestFactories.CreateMigrationFile(filename: "30_Newer.sql", release: "Release 3.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().HaveCount(1);
        result[0].Filename.Should().Be("10_Old.sql");
    }

    [Fact]
    public void MultipleOlderReleases_AllDetected()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationFile(filename: "10_B.sql", release: "Release 1.1"),
            TestFactories.CreateMigrationFile(filename: "10_C.sql", release: "Release 1.2")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void OnlyUnclearRecords_ReturnsEmpty()
    {
        // Records that are not in Migrated/Ok state should not count
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0",
                status: MigrationStatus.Failed)
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void OnlyErrorRecords_ReturnsEmpty()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0",
                status: MigrationStatus.Pending)
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void HighestReleaseDeterminedByStringComparison()
    {
        // "Release 2.0" > "Release 10.0" alphabetically — tests the comparison behavior
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_A.sql", release: "Release 10.0")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_R1.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationRecord(filename: "10_R2.sql", release: "Release 2.0")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        // "Release 2.0" is highest alphabetically, "Release 10.0" < "Release 2.0" alphabetically
        result.Should().HaveCount(1);
    }

    [Fact]
    public void MultipleSuccessfulRecords_UsesHighestRelease()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Missed.sql", release: "Release 1.5")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_R1.sql", release: "Release 1.0"),
            TestFactories.CreateMigrationRecord(filename: "10_R2.sql", release: "Release 2.0"),
            TestFactories.CreateMigrationRecord(filename: "10_R3.sql", release: "Release 1.5")
        };

        var result = MigrationService.DetectOutOfOrderFiles(files, records);

        // Highest is "Release 2.0", "Release 1.5" < "Release 2.0" => out of order
        result.Should().HaveCount(1);
    }
}
