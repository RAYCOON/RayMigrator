// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for F7 — MigrationAlreadyRunningException via dedicated ResultCode -2.
/// Tests the GetValidatedTemplateResponseFromExecuteScalar method and the ResultCode convention.
/// </summary>
public class MigrationAlreadyRunningTests
{
    private static readonly Template TestTemplate = new()
    {
        TemplateType = TemplateType.Repository_MigrationRun_Insert,
        DatabaseType = "SqlServer",
        Filename = "Repository_MigrationRun_Insert.sql"
    };

    [Fact]
    public void GetValidatedTemplateResponse_WithResultCodeMinusTwo_ThrowsTemplateResultExceptionWithCodeMinusTwo()
    {
        // Arrange: SQL template returns "-2,MigrationRun for Product [Test] is currently in progress..."
        string scalarResult = "-2,MigrationRun for Product [Test] with Id [1] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate=100] are not allowed!";

        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>()
            .Where(ex => ex.ResultCode == TemplateResultCode.MigrationAlreadyRunning);
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithResultCodeMinusOne_ThrowsTemplateResultExceptionWithCodeMinusOne()
    {
        // Arrange: SQL template returns "-1,Failed to create MigrationRun..."
        string scalarResult = "-1,Failed to create MigrationRun for ProductId [1]";

        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>()
            .Where(ex => ex.ResultCode == TemplateResultCode.GeneralError);
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithPositiveResultCode_ReturnsTemplateResponse()
    {
        // Arrange: SQL template returns success "42,MigrationRun with Id [42] successfully created"
        string scalarResult = "42,MigrationRun with Id [42] successfully created for ProductId [1]";

        // Act
        var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        response.ResultCode.Should().Be(42);
        response.ResultMessage.Should().Contain("successfully created");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithNull_ThrowsTemplateResultException()
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(null, TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>()
            .WithMessage("*returned [null]*");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithEmptyString_ThrowsTemplateResultException()
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar("", TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>()
            .WithMessage("*empty string*");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithNonNumericResult_ThrowsTemplateResultException()
    {
        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar("abc,some message", TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>()
            .WithMessage("*NOT possible*");
    }

    [Fact]
    public void GetValidatedTemplateResponse_WithResultCodeZero_ReturnsSuccess()
    {
        // Arrange: ResultCode 0 means success (e.g., no interrupted migration found)
        string scalarResult = "0,No interrupted migration found";

        // Act
        var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        response.ResultCode.Should().Be(0);
    }

    [Fact]
    public void GetValidatedTemplateResponse_MariaDbLockFailMinusTwo_ThrowsWithCodeMinusTwo()
    {
        // Arrange: MariaDB/MySQL lock failure returns -2
        string scalarResult = "-2,Could not acquire migration lock for ProductId [1] environment [Docker]. Another migration may be in progress.";

        // Act
        Action act = () => TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(scalarResult, TestTemplate);

        // Assert
        act.Should().Throw<TemplateResultException>()
            .Where(ex => ex.ResultCode == TemplateResultCode.MigrationAlreadyRunning);
    }
}
