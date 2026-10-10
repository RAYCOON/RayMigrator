using AwesomeAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for the template result contract 'code[,code...],message' (#27). The first code stays the
/// ResultCode; further codes carry template-specific information such as RepositoryWasCreated.
/// </summary>
public class TemplateResponseMultiCodeTests
{
    private static readonly Template TestTemplate = new()
    {
        TemplateType = TemplateType.Repository_CheckCreate,
        DatabaseType = "SqlServer",
        Filename = "Repository_CheckCreate.sql"
    };

    [Fact]
    public void GetValidatedTemplateResponse_WithTwoCodes_ParsesBothCodesAndTheMessage()
    {
        // Arrange
        string scalarResult = "5,1,RayMigrator repository-tables with master data and new VersionId [5] successfully created";

        // Act
        var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        response.ResultCode.Should().Be(5, because: "the first code stays the result code");
        response.ResultCodes.Should().Equal(5, 1);
        response.ResultMessage.Should().Be("RayMigrator repository-tables with master data and new VersionId [5] successfully created");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithSingleCode_HasOneEntryInResultCodes()
    {
        // Act
        var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar("42,MigrationRun created", TestTemplate);

        // Assert
        response.ResultCodes.Should().Equal(42);
        response.ResultMessage.Should().Be("MigrationRun created");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithCommaInMessage_KeepsTheMessageIntact()
    {
        // Act
        var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar("5,text with, a comma", TestTemplate);

        // Assert
        response.ResultCodes.Should().Equal(5);
        response.ResultMessage.Should().Be("text with, a comma", because: "the split stops at the first token that is not an integer");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithCodeOnly_ReturnsEmptyMessage()
    {
        // Act
        var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar("7", TestTemplate);

        // Assert
        response.ResultCode.Should().Be(7);
        response.ResultCodes.Should().Equal(7);
        response.ResultMessage.Should().BeEmpty();
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithWhitespaceAroundCodes_TrimsEveryToken()
    {
        // Act
        var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(" 5 , 0 , message ", TestTemplate);

        // Assert
        response.ResultCodes.Should().Equal(5, 0);
        response.ResultMessage.Should().Be("message");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithNegativeFirstCodeAndSecondCode_ThrowsWithTheFirstCode()
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar("-10,1,repository incomplete", TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>().Which.ResultCode.Should().Be(-10);
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithNonNumericFirstToken_Throws()
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar("created,5", TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>(because: "the response has to start with an integer code");
    }

    [Fact]
    public void ToString_WithAdditionalCodes_ListsThem()
    {
        // Arrange
        var response = new TemplateResponse { ResultCode = 5, ResultCodes = [5, 1], ResultMessage = "created" };

        // Act
        string text = response.ToString();

        // Assert
        text.Should().Be("ResultCode: 5 (codes: 5, 1), ResultMessage: created");
    }
}
