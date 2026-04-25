using FluentAssertions;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P3-1: Model class tests.
/// </summary>
public class MigrationFileInfoModelTests
{
    [Fact]
    public void FileUpBlocksTotal_CalculatesFromSqlBlocksCount()
    {
        var file = new MigrationFileInfo
        {
            SqlBlocks = new List<string> { "SELECT 1", "SELECT 2", "SELECT 3" }
        };

        file.FileUpBlocksTotal.Should().Be(3);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var file = new MigrationFileInfo();

        file.UseTransaction.Should().BeTrue();
        file.RunAlways.Should().BeFalse();
        file.Filename.Should().BeEmpty();
        file.SqlBlocks.Should().BeEmpty();
        file.FileUpBlocksTotal.Should().Be(0);
    }
}

/// <summary>
/// P3-2: Custom Exception tests.
/// </summary>
public class CustomExceptionTests
{
    [Fact]
    public void ConfigurationValidationException_ContainsAbortMessage()
    {
        var ex = new ConfigurationValidationException("test error");

        ex.Message.Should().StartWith(ConfigurationValidationException.AbortMessage);
        ex.Message.Should().Contain("test error");
    }

    [Fact]
    public void ConfigurationValidationException_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ConfigurationValidationException("test error", inner);

        ex.InnerException.Should().Be(inner);
        ex.Message.Should().Contain("test error");
    }

    [Fact]
    public void ApplicationStartupException_ContainsAbortMessage()
    {
        var ex = new ApplicationStartupException("startup fail");

        ex.Message.Should().StartWith(ApplicationStartupException.AbortMessage);
    }

    [Fact]
    public void TemplateExecutionException_ContainsAbortMessage()
    {
        var ex = new TemplateExecutionException("execution fail");

        ex.Message.Should().Contain("execution fail");
    }

    [Fact]
    public void MigrationFileParsingException_ContainsAbortMessage()
    {
        var ex = new MigrationFileParsingException("parse fail");

        ex.Message.Should().StartWith(MigrationFileParsingException.AbortMessage);
        ex.Message.Should().Contain("parse fail");
    }

    [Fact]
    public void DatabaseTransientException_StoresAttemptsMade()
    {
        var ex = new DatabaseTransientException("transient fail", 3);

        ex.AttemptsMade.Should().Be(3);
    }

    [Fact]
    public void MigrationAlreadyRunningException_StoresProductId()
    {
        var ex = new MigrationAlreadyRunningException("already running", 42);

        ex.ProductId.Should().Be(42);
    }

    [Fact]
    public void TemplateResultException_ContainsMessage()
    {
        var ex = new TemplateResultException("negative result");

        ex.Message.Should().Contain("negative result");
    }

    [Fact]
    public void TemplateResultException_DefaultResultCode_IsMinusOne()
    {
        var ex = new TemplateResultException("negative result");

        ex.ResultCode.Should().Be(-1);
    }

    [Fact]
    public void TemplateResultException_WithResultCode_StoresResultCode()
    {
        var ex = new TemplateResultException("parallel run detected", -2);

        ex.ResultCode.Should().Be(-2);
        ex.Message.Should().Contain("parallel run detected");
    }

    [Fact]
    public void TemplateResultException_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new TemplateResultException("negative result", inner);

        ex.InnerException.Should().Be(inner);
        ex.Message.Should().Contain("negative result");
    }

    [Fact]
    public void MigrationHashValidationException_ContainsMessageWithoutAbortPrefix()
    {
        var ex = new MigrationHashValidationException("hash mismatch");

        ex.Message.Should().Be("hash mismatch");
    }

    [Fact]
    public void MigrationHashValidationException_WithInnerException_SetsInnerException()
    {
        var inner = new Exception("inner");
        var ex = new MigrationHashValidationException("hash mismatch", inner);

        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void MigrationExecutionException_ContainsMessageWithoutAbortPrefix()
    {
        var ex = new MigrationExecutionException("execution failed");

        ex.Message.Should().Be("execution failed");
    }

    [Fact]
    public void MigrationExecutionException_WithInnerException_SetsInnerException()
    {
        var inner = new Exception("inner");
        var ex = new MigrationExecutionException("execution failed", inner);

        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void RayMigratorInternalException_ContainsAbortMessage()
    {
        var ex = new RayMigratorInternalException("internal error");

        ex.Message.Should().StartWith(RayMigratorInternalException.AbortMessage);
        ex.Message.Should().Contain("internal error");
    }

    [Fact]
    public void RayMigratorInternalException_WithInnerException_SetsInnerException()
    {
        var inner = new Exception("inner");
        var ex = new RayMigratorInternalException("internal error", inner);

        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void NotYetImplementedException_ContainsFeatureName()
    {
        var ex = new NotYetImplementedException("SQLite Support");

        ex.Message.Should().StartWith(NotYetImplementedException.AbortMessage);
        ex.Message.Should().Contain("'SQLite Support'");
        ex.Message.Should().Contain("planned for a future release");
    }

    [Fact]
    public void DatabaseParameterException_StoresParameterCount()
    {
        var ex = new DatabaseParameterException("param error", 5);

        ex.ParameterCount.Should().Be(5);
        ex.Message.Should().StartWith(DatabaseParameterException.AbortMessage);
    }

    [Fact]
    public void DatabaseParameterException_WithParameterCountAndInnerException()
    {
        var inner = new Exception("inner");
        var ex = new DatabaseParameterException("param error", 3, inner);

        ex.ParameterCount.Should().Be(3);
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void DatabaseParameterException_MessageOnly_DefaultParameterCount()
    {
        var ex = new DatabaseParameterException("param error");

        ex.ParameterCount.Should().Be(0);
        ex.Message.Should().Contain("param error");
    }

    [Fact]
    public void MigrationRecoveryException_StoresMigrationRunId()
    {
        var ex = new MigrationRecoveryException("recovery fail", 10);

        ex.MigrationRunId.Should().Be(10);
        ex.Message.Should().StartWith(MigrationRecoveryException.AbortMessage);
    }

    [Fact]
    public void MigrationRecoveryException_StoresMigrationRunIdAndMigrationRecordId()
    {
        var ex = new MigrationRecoveryException("recovery fail", 10, 20);

        ex.MigrationRunId.Should().Be(10);
        ex.MigrationRecordId.Should().Be(20);
    }

    [Fact]
    public void MigrationRecoveryException_WithInnerException()
    {
        var inner = new Exception("inner");
        var ex = new MigrationRecoveryException("recovery fail", inner);

        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void MigrationAlreadyRunningException_ThreeParamConstructor_StoresExistingMigrationRunId()
    {
        var ex = new MigrationAlreadyRunningException("already running", 42, 99);

        ex.ProductId.Should().Be(42);
        ex.ExistingMigrationRunId.Should().Be(99);
        ex.Message.Should().StartWith(MigrationAlreadyRunningException.AbortMessage);
    }

    [Fact]
    public void DatabaseTransientException_StoresLastErrorCode()
    {
        var ex = new DatabaseTransientException("transient fail", 3, "1205");

        ex.AttemptsMade.Should().Be(3);
        ex.LastErrorCode.Should().Be("1205");
    }

    [Fact]
    public void DatabaseTransientException_WithInnerException()
    {
        var inner = new TimeoutException("timeout");
        var ex = new DatabaseTransientException("transient fail", 2, inner);

        ex.AttemptsMade.Should().Be(2);
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void DatabaseTransientException_WithLastErrorCodeAndInnerException()
    {
        var inner = new TimeoutException("timeout");
        var ex = new DatabaseTransientException("transient fail", 2, "1205", inner);

        ex.AttemptsMade.Should().Be(2);
        ex.LastErrorCode.Should().Be("1205");
        ex.InnerException.Should().Be(inner);
    }
}
