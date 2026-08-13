using AwesomeAssertions;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0-4: File Classification tests.
/// Rollback files executed as forward migration = double data loss.
/// </summary>
public class FileClassificationTests
{
    private const string DefaultExt = "sql";
    private const string DefaultRollback = "rollback";

    #region IsRollbackFile

    [Fact]
    public void IsRollbackFile_RollbackFile_ReturnsTrue()
    {
        MigrationService.IsRollbackFile("20_Insert.rollback.sql", DefaultRollback, DefaultExt)
            .Should().BeTrue();
    }

    [Fact]
    public void IsRollbackFile_NormalFile_ReturnsFalse()
    {
        MigrationService.IsRollbackFile("20_Insert.sql", DefaultRollback, DefaultExt)
            .Should().BeFalse();
    }

    [Fact]
    public void IsRollbackFile_CaseInsensitive_ReturnsTrue()
    {
        MigrationService.IsRollbackFile("20_Insert.ROLLBACK.sql", DefaultRollback, DefaultExt)
            .Should().BeTrue();
    }

    [Fact]
    public void IsRollbackFile_CustomPreExtension_ReturnsTrue()
    {
        MigrationService.IsRollbackFile("20_Insert.undo.sql", "undo", DefaultExt)
            .Should().BeTrue();
    }

    #endregion

    #region IsEnvironmentSpecificFile

    [Fact]
    public void IsEnvironmentSpecific_WithEnvironment_ReturnsTrue()
    {
        MigrationService.IsEnvironmentSpecificFile("01_login.Docker.sql", DefaultExt)
            .Should().BeTrue();
    }

    [Fact]
    public void IsEnvironmentSpecific_WithoutEnvironment_ReturnsFalse()
    {
        MigrationService.IsEnvironmentSpecificFile("10_CreateModel.sql", DefaultExt)
            .Should().BeFalse();
    }

    [Fact]
    public void IsEnvironmentSpecific_MultipleDotsInName_ReturnsTrue()
    {
        MigrationService.IsEnvironmentSpecificFile("01_v1.2.Docker.sql", DefaultExt)
            .Should().BeTrue();
    }

    #endregion

    #region IsForEnvironment

    [Fact]
    public void IsForEnvironment_MatchingEnvironment_ReturnsTrue()
    {
        MigrationService.IsForEnvironment("01_login.Docker.sql", "Docker", DefaultExt)
            .Should().BeTrue();
    }

    [Fact]
    public void IsForEnvironment_NonMatchingEnvironment_ReturnsFalse()
    {
        MigrationService.IsForEnvironment("01_login.Docker.sql", "Production", DefaultExt)
            .Should().BeFalse();
    }

    [Fact]
    public void IsForEnvironment_CaseInsensitive_ReturnsTrue()
    {
        MigrationService.IsForEnvironment("01_login.docker.sql", "Docker", DefaultExt)
            .Should().BeTrue();
    }

    #endregion

    #region GetRollbackFilename

    [Fact]
    public void GetRollbackFilename_StandardFile_ConstructsCorrectly()
    {
        MigrationService.GetRollbackFilename("20_Insert.sql", DefaultRollback, DefaultExt)
            .Should().Be("20_Insert.rollback.sql");
    }

    [Fact]
    public void GetRollbackFilename_FileWithDotsInName_ConstructsCorrectly()
    {
        // Path.GetFileNameWithoutExtension("01_v1.2.sql") returns "01_v1.2"
        // which means the result should be "01_v1.2.rollback.sql"
        var result = MigrationService.GetRollbackFilename("01_v1.2.sql", DefaultRollback, DefaultExt);
        result.Should().Be("01_v1.2.rollback.sql");
    }

    #endregion
}
