// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class EnvironmentSkeletonGeneratorTests
{
    [Fact]
    public void Generate_ContainsRepositoryConnectionString()
    {
        var model = TestModelFactory.CreateValidModel();
        var json = EnvironmentSkeletonGenerator.Generate(model);

        json.Should().Contain("REPO_CONNECTION_STRING");
    }

    [Fact]
    public void Generate_WithDatabaseLogging_ContainsDbLogConnectionString()
    {
        var model = TestModelFactory.CreateValidModel();
        model.DatabaseLogging = new DatabaseLoggingModel { ConnectionString = "" };

        var json = EnvironmentSkeletonGenerator.Generate(model);
        json.Should().Contain("DBLOG_CONNECTION_STRING");
    }

    [Fact]
    public void Generate_PreservesExistingEnvPlaceholders()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:MY_CUSTOM_CONN}";

        var json = EnvironmentSkeletonGenerator.Generate(model);
        json.Should().Contain("{ENV:MY_CUSTOM_CONN}");
    }

    [Fact]
    public void Generate_ProductTargetConnectionStrings_UsesSanitizedNames()
    {
        var model = TestModelFactory.CreateValidModel();
        var json = EnvironmentSkeletonGenerator.Generate(model);

        json.Should().Contain("MYPRODUCT_BACKEND_MAINDB_CONNECTION_STRING");
    }

    [Fact]
    public void Generate_EmptyModel_NoProducts_ContainsRepository()
    {
        var model = new ConfigurationModel();
        var json = EnvironmentSkeletonGenerator.Generate(model);

        json.Should().Contain("Repository");
        json.Should().NotContain("Products");
    }
}
