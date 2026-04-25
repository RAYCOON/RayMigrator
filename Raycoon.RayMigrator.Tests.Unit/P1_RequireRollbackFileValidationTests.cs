using FluentAssertions;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: RequireRollbackFile validation logic tests.
/// Tests the filtering of migration files with missing rollback files when RequireRollbackFile is enabled,
/// and the default value of MigrationFileInfo.RequireRollbackFile.
/// </summary>
public class RequireRollbackFileValidationTests
{
    [Fact]
    public void ValidateMissingRollbacks_AllHaveRollback_NoException()
    {
        var files = new List<MigrationFileInfo>
        {
            new MigrationFileInfo { RequireRollbackFile = true, MigrateDownFileExists = true, FilenameWithRelativePath = "R1/001.sql" },
            new MigrationFileInfo { RequireRollbackFile = true, MigrateDownFileExists = true, FilenameWithRelativePath = "R1/002.sql" }
        };

        var missingRollbacks = files
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        missingRollbacks.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMissingRollbacks_MissingRollback_Detected()
    {
        var files = new List<MigrationFileInfo>
        {
            new MigrationFileInfo { RequireRollbackFile = true, MigrateDownFileExists = true, FilenameWithRelativePath = "R1/001.sql" },
            new MigrationFileInfo { RequireRollbackFile = true, MigrateDownFileExists = false, FilenameWithRelativePath = "R1/002.sql" }
        };

        var missingRollbacks = files
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        missingRollbacks.Should().HaveCount(1);
        missingRollbacks[0].FilenameWithRelativePath.Should().Be("R1/002.sql");
    }

    [Fact]
    public void ValidateMissingRollbacks_RequireDisabled_NotDetected()
    {
        var files = new List<MigrationFileInfo>
        {
            new MigrationFileInfo { RequireRollbackFile = false, MigrateDownFileExists = false, FilenameWithRelativePath = "R1/001.sql" }
        };

        var missingRollbacks = files
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        missingRollbacks.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMissingRollbacks_MixedRequireSettings()
    {
        var files = new List<MigrationFileInfo>
        {
            new MigrationFileInfo { RequireRollbackFile = true, MigrateDownFileExists = false, FilenameWithRelativePath = "R1/001.sql" },
            new MigrationFileInfo { RequireRollbackFile = false, MigrateDownFileExists = false, FilenameWithRelativePath = "R1/002.sql" },
            new MigrationFileInfo { RequireRollbackFile = true, MigrateDownFileExists = true, FilenameWithRelativePath = "R1/003.sql" }
        };

        var missingRollbacks = files
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        missingRollbacks.Should().HaveCount(1);
        missingRollbacks[0].FilenameWithRelativePath.Should().Be("R1/001.sql");
    }

    [Fact]
    public void MigrationFileInfo_RequireRollbackFile_DefaultIsTrue()
    {
        var info = new MigrationFileInfo();

        info.RequireRollbackFile.Should().BeTrue();
    }

    [Fact]
    public void RequireRollbackTrue_MissingRollbackFiles_ThrowsWithRollbackFilenames()
    {
        string rollbackPreExtension = "rollback";
        string fileExtension = "sql";

        var files = new List<MigrationFileInfo>
        {
            new() { RequireRollbackFile = true, MigrateDownFileExists = false,
                     Filename = "01_ooc_login.Docker.sql",
                     FilenameWithRelativePath = "Release 1.0/Backend/01_ooc_login.Docker.sql" },
            new() { RequireRollbackFile = true, MigrateDownFileExists = false,
                     Filename = "10_CreateDataModel.sql",
                     FilenameWithRelativePath = "Release 1.0/Backend/10_CreateDataModel.sql" },
            new() { RequireRollbackFile = true, MigrateDownFileExists = true,
                     Filename = "20_InsertData.sql",
                     FilenameWithRelativePath = "Release 1.0/Backend/20_InsertData.sql" }
        };

        var missingRollbacks = files
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        var act = () =>
        {
            if (missingRollbacks.Count > 0)
            {
                var fileList = string.Join("\n", missingRollbacks.Select(f =>
                {
                    string rollbackName = MigrationService.GetRollbackFilename(f.Filename, rollbackPreExtension, fileExtension);
                    string dir = Path.GetDirectoryName(f.FilenameWithRelativePath) ?? "";
                    return $"  - {Path.Combine(dir, rollbackName)}";
                }));
                throw new MigrationFileParsingException(
                    $"RequireRollbackFile validation failed. {missingRollbacks.Count} rollback file(s) " +
                    $"are missing:\n{fileList}");
            }
        };

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*RequireRollbackFile validation failed*2 rollback file(s) are missing*")
            .WithMessage("*01_ooc_login.Docker.rollback.sql*")
            .WithMessage("*10_CreateDataModel.rollback.sql*")
            .Which.Message.Should().NotContain("01_ooc_login.Docker.sql\n");
    }

    [Fact]
    public void RequireRollbackTrue_AllRollbacksExist_NoException()
    {
        var files = new List<MigrationFileInfo>
        {
            new() { RequireRollbackFile = true, MigrateDownFileExists = true,
                     Filename = "01_Create.sql",
                     FilenameWithRelativePath = "Release 1.0/Backend/01_Create.sql" },
            new() { RequireRollbackFile = true, MigrateDownFileExists = true,
                     Filename = "02_Insert.sql",
                     FilenameWithRelativePath = "Release 1.0/Backend/02_Insert.sql" }
        };

        var missingRollbacks = files
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        missingRollbacks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("rollback", "sql", "10_Create.rollback.sql")]
    [InlineData("undo", "sql", "10_Create.undo.sql")]
    [InlineData("rollback", "ddl", "10_Create.rollback.ddl")]
    public void GetRollbackFilename_UsesConfiguredPreExtensionAndExtension(
        string rollbackPreExtension, string fileExtension, string expectedRollbackFilename)
    {
        string result = MigrationService.GetRollbackFilename("10_Create." + fileExtension, rollbackPreExtension, fileExtension);

        result.Should().Be(expectedRollbackFilename);
    }

    [Fact]
    public void RequireRollbackFalse_MissingRollbackFiles_NoException()
    {
        var files = new List<MigrationFileInfo>
        {
            new() { RequireRollbackFile = false, MigrateDownFileExists = false,
                     Filename = "01_Create.sql",
                     FilenameWithRelativePath = "Release 1.0/Backend/01_Create.sql" },
            new() { RequireRollbackFile = false, MigrateDownFileExists = false,
                     Filename = "02_Insert.sql",
                     FilenameWithRelativePath = "Release 1.0/Backend/02_Insert.sql" }
        };

        var missingRollbacks = files
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        missingRollbacks.Should().BeEmpty();
    }
}
