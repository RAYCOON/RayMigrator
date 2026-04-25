
using FluentAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for ResultCode catalog — verifies that each negative ResultCode from the catalog
/// is correctly propagated through GetValidatedTemplateResponseFromExecuteScalar,
/// and that unknown negative codes throw UndefinedTemplateResultException.
/// </summary>
public class TemplateResultCodeTests
{
    private static readonly Template TestTemplate = new()
    {
        TemplateType = TemplateType.Repository_CheckCreate,
        DatabaseType = "SqlServer",
        Filename = "Repository_CheckCreate.sql"
    };

    #region Known ResultCodes

    [Theory]
    [InlineData("-10,RayMigrator repository incomplete or corrupt. Repository contains [5] tables instead of [10].", -10)]
    [InlineData("-11,RayMigrator repository incomplete or corrupt. Repository contains [3] tables instead of the expected amount of [0].", -11)]
    [InlineData("-12,Multiple [MigratorMeta]-entries found for RepositoryVersion [1] RepositoryDatabaseType [SqlServer] RayMigratorVersion [3.0.0].", -12)]
    [InlineData("-20,Product with empty name [NULL] is not allowed!", -20)]
    [InlineData("-30,MigrationRun with Id [42] does not exist", -30)]
    [InlineData("-31,MigrationRun [42] not found or not in Running state", -31)]
    [InlineData("-40,Migration with Id [99] does not exist", -40)]
    [InlineData("-50,Environment with empty name [NULL] is not allowed!", -50)]
    public void GetValidatedTemplateResponse_WithKnownNegativeCode_ThrowsTemplateResultExceptionWithCorrectCode(
        string scalarResult, int expectedCode)
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>()
            .Which.ResultCode.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("-10,RayMigrator repository incomplete or corrupt. Repository contains [5] tables instead of [10].")]
    [InlineData("-11,RayMigrator repository incomplete or corrupt. Repository contains [3] tables instead of the expected amount of [0].")]
    [InlineData("-12,Multiple [MigratorMeta]-entries found for RepositoryVersion [1] RepositoryDatabaseType [SqlServer] RayMigratorVersion [3.0.0].")]
    [InlineData("-20,Product with empty name [NULL] is not allowed!")]
    [InlineData("-30,MigrationRun with Id [42] does not exist")]
    [InlineData("-31,MigrationRun [42] not found or not in Running state")]
    [InlineData("-40,Migration with Id [99] does not exist")]
    [InlineData("-50,Environment with empty name [NULL] is not allowed!")]
    [InlineData("-1,General template error")]
    [InlineData("-2,MigrationRun already running")]
    public void GetValidatedTemplateResponse_WithKnownNegativeCode_ThrowsTemplateResultException_NotUndefined(
        string scalarResult)
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert — known codes must throw TemplateResultException, NOT UndefinedTemplateResultException
        act.Should().Throw<TemplateResultException>()
            .And.Should().NotBeOfType<UndefinedTemplateResultException>();
    }

    #endregion Known ResultCodes

    #region Unknown ResultCodes

    [Theory]
    [InlineData("-99,User-defined error from custom template")]
    [InlineData("-500,Custom template failure")]
    [InlineData("-3,Some other error")]
    [InlineData("-100,Another unknown error")]
    public void GetValidatedTemplateResponse_WithUnknownNegativeCode_ThrowsUndefinedTemplateResultException(
        string scalarResult)
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        act.Should().Throw<UndefinedTemplateResultException>();
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithUnknownNegativeCode_PreservesResultCode()
    {
        // Arrange
        string scalarResult = "-99,User-defined error";

        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        act.Should().Throw<UndefinedTemplateResultException>()
            .Which.ResultCode.Should().Be(-99);
    }

    #endregion Unknown ResultCodes

    #region IsKnown

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-2, true)]
    [InlineData(-10, true)]
    [InlineData(-11, true)]
    [InlineData(-12, true)]
    [InlineData(-20, true)]
    [InlineData(-30, true)]
    [InlineData(-31, true)]
    [InlineData(-40, true)]
    [InlineData(-50, true)]
    [InlineData(-3, false)]
    [InlineData(-51, false)]
    [InlineData(-99, false)]
    [InlineData(-500, false)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(1001, false)]
    public void IsKnown_ReturnsExpectedResult(int code, bool expected)
    {
        TemplateResultCode.IsKnown(code).Should().Be(expected);
    }

    #endregion IsKnown

    #region ExtractErrorCode via MigrationFileParsingException

    [Fact]
    public void MigrationFileParsingException_WithErrorCode_PreservesErrorCode()
    {
        // Arrange
        var ex = new MigrationFileParsingException("test error", TemplateResultCode.RequireRollbackFileValidationFailed);

        // Assert
        ex.ErrorCode.Should().Be(TemplateResultCode.RequireRollbackFileValidationFailed);
    }

    [Fact]
    public void MigrationFileParsingException_WithoutErrorCode_HasNullErrorCode()
    {
        // Arrange
        var ex = new MigrationFileParsingException("test error");

        // Assert
        ex.ErrorCode.Should().BeNull();
    }

    #endregion ExtractErrorCode via MigrationFileParsingException

    #region UndefinedTemplateResultException Inheritance

    [Fact]
    public void UndefinedTemplateResultException_IsTemplateResultException()
    {
        // Arrange
        var ex = new UndefinedTemplateResultException("test", -99);

        // Assert — inherits from TemplateResultException
        ex.Should().BeAssignableTo<TemplateResultException>();
        ex.ResultCode.Should().Be(-99);
    }

    [Fact]
    public void UndefinedTemplateResultException_CaughtByTemplateResultExceptionHandler()
    {
        // Act & Assert — catch(TemplateResultException) should catch it
        Action act = () => throw new UndefinedTemplateResultException("test", -99);

        act.Should().Throw<TemplateResultException>();
    }

    #endregion UndefinedTemplateResultException Inheritance
}
