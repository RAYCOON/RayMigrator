// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for flat migration directory layout ambiguity detection.
/// When a product has a single TargetGroup, migration files may be placed directly under
/// release directories (flat layout) instead of under a TG subdirectory (traditional layout).
/// Mixing both layouts in the same release is an error that must be detected early.
/// </summary>
public class FlatLayoutAmbiguityTests
{
    private static MigrationFileInfo CreateFile(string relPath, string release)
    {
        return new MigrationFileInfo
        {
            FilenameWithRelativePath = relPath,
            ReleaseVersion = release,
            Filename = Path.GetFileName(relPath)
        };
    }

    // ──────────────────────────────────────────────────────
    // All-flat layout — no exception expected
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_AllFlatFiles_DoesNotThrow()
    {
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/20_Insert.sql", "Release 1.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────
    // All-traditional layout — no exception expected
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_AllTraditionalFiles_DoesNotThrow()
    {
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/Backend/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/Backend/20_Insert.sql", "Release 1.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────
    // Mixed layout in the same release — must throw
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_MixedLayouts_SameRelease_ThrowsConfigurationValidationException()
    {
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),         // flat
            CreateFile("Release 1.0/Backend/20_Insert.sql", "Release 1.0")  // traditional
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().Throw<ConfigurationValidationException>();
    }

    [Fact]
    public void ValidateFlatLayoutAmbiguity_MixedLayouts_ThrowsMessageContainsRelease()
    {
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 2.0/10_Create.sql", "Release 2.0"),
            CreateFile("Release 2.0/Backend/20_Insert.sql", "Release 2.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "MyProduct");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Release 2.0*");
    }

    [Fact]
    public void ValidateFlatLayoutAmbiguity_MixedLayouts_ThrowsMessageContainsProduct()
    {
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/Backend/20_Insert.sql", "Release 1.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "MyProduct");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*MyProduct*");
    }

    [Fact]
    public void ValidateFlatLayoutAmbiguity_MixedLayouts_ThrowsMessageContainsTgAlias()
    {
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/Backend/20_Insert.sql", "Release 1.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Backend*");
    }

    // ──────────────────────────────────────────────────────
    // Different releases may independently use flat or traditional
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_DifferentReleases_DifferentLayouts_DoesNotThrow()
    {
        // Release 1.0 uses flat, Release 2.0 uses traditional — no mixing within a single release
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),
            CreateFile("Release 2.0/Backend/20_Insert.sql", "Release 2.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFlatLayoutAmbiguity_MixedInOneRelease_CleanInAnother_ThrowsOnlyForMixedRelease()
    {
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),                 // flat
            CreateFile("Release 1.0/Backend/20_Insert.sql", "Release 1.0"),         // traditional → mixed
            CreateFile("Release 2.0/Backend/30_AddColumn.sql", "Release 2.0")       // traditional only → ok
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Release 1.0*");
    }

    // ──────────────────────────────────────────────────────
    // TG alias matching is case-insensitive
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_TgAliasMatchIsCaseInsensitive()
    {
        // "backend" in the path should match singleTgAlias "Backend"
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/backend/20_Insert.sql", "Release 1.0")   // lowercase
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().Throw<ConfigurationValidationException>();
    }

    // ──────────────────────────────────────────────────────
    // Empty file list — no exception
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_EmptyFileList_DoesNotThrow()
    {
        var files = new List<MigrationFileInfo>();

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────
    // Segment[1] is NOT the TG alias (different subdir name) — treated as flat
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_SubdirNotMatchingTgAlias_TreatedAsFlat()
    {
        // "subdir" is not "Backend", so both files are "flat" from the validation perspective
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/subdir/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/subdir/20_Insert.sql", "Release 1.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────
    // Single flat file + single traditional file in same release — mixed
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_OneFlatAndOneTraditional_SameRelease_ThrowsConfigurationValidationException()
    {
        // Minimum ambiguity case: exactly one flat and one traditional file in the same release.
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/10_Create.sql", "Release 1.0"),          // flat
            CreateFile("Release 1.0/Backend/20_Insert.sql", "Release 1.0")   // traditional
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().Throw<ConfigurationValidationException>();
    }

    // ──────────────────────────────────────────────────────
    // Multiple non-TG subdirs — all treated as flat, no throw
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_MultipleNonTgSubdirs_AllTreatedAsFlat_DoesNotThrow()
    {
        // "scripts" and "helpers" are neither the TG alias nor match it, so every file is flat.
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/scripts/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/helpers/20_Insert.sql", "Release 1.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "Backend", "TestProduct");

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────
    // TG alias with dots — traditional layout still detected
    // ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateFlatLayoutAmbiguity_TgAliasWithDots_TraditionalLayoutDetected_DoesNotThrow()
    {
        // segment[1] = "DB.Server" matches singleTgAlias "DB.Server" → treated as traditional.
        var files = new List<MigrationFileInfo>
        {
            CreateFile("Release 1.0/DB.Server/10_Create.sql", "Release 1.0"),
            CreateFile("Release 1.0/DB.Server/20_Insert.sql", "Release 1.0")
        };

        var act = () => MigrationService.ValidateFlatLayoutAmbiguity(files, "DB.Server", "TestProduct");

        act.Should().NotThrow();
    }

    // ══════════════════════════════════════════════════════
    // ValidateTargetGroupAliasCasing tests
    // ══════════════════════════════════════════════════════

    private static ProductOptions CreateProductOptions(params string[] tgAliases)
    {
        return new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = tgAliases.Select(a => new TargetGroupOptions { Alias = a }).ToList()
        };
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_ExactMatch_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "Backend"));

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_CaseMismatch_ThrowsConfigurationValidationException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "backend"));

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().Throw<ConfigurationValidationException>()
                .WithMessage("*backend*Backend*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_NoSubdirectories_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0"));

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_UnrelatedSubdirectory_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "SomethingElse"));

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_EmptyTargetGroups_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "backend"));

            var productOptions = new ProductOptions
            {
                Alias = "TestProduct",
                TargetGroups = new List<TargetGroupOptions>()
            };

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_CaseMismatch_MessageContainsDirectoryAndAlias()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "BACKEND"));

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().Throw<ConfigurationValidationException>()
                .WithMessage("*BACKEND*")
                .WithMessage("*Backend*")
                .WithMessage("*Release 1.0*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_MultipleTgs_OneCaseMismatch_ThrowsForMismatch()
    {
        // "Backend" and "Frontend" both configured. "backend" matches "Backend" case-insensitively
        // but differs in case → must throw even though "Frontend" has no directory at all.
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "backend"));

            var productOptions = CreateProductOptions("Backend", "Frontend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().Throw<ConfigurationValidationException>()
                .WithMessage("*backend*Backend*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_NullTargetGroups_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "backend"));

            var productOptions = new ProductOptions
            {
                Alias = "TestProduct",
                TargetGroups = null
            };

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_MultipleReleases_MismatchOnlyInSecond_Throws()
    {
        // Release 1.0 has the exact casing; Release 2.0 has a lowercase mismatch.
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "Backend"));  // exact match
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 2.0", "backend"));  // case mismatch

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().Throw<ConfigurationValidationException>()
                .WithMessage("*backend*Backend*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_NoReleaseDirectories_DoesNotThrow()
    {
        // An empty root directory has no release subdirectories to iterate.
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetGroupAliasCasing_CaseMismatch_MessageSuggestsRenamingDirectory()
    {
        // The error message must guide the user to rename the directory to match the configured alias.
        var tempDir = Path.Combine(Path.GetTempPath(), $"rm_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Release 1.0", "backend"));

            var productOptions = CreateProductOptions("Backend");

            var act = () => MigrationService.ValidateTargetGroupAliasCasing(tempDir, productOptions);

            act.Should().Throw<ConfigurationValidationException>()
                .WithMessage("*Rename the directory to [Backend]*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
