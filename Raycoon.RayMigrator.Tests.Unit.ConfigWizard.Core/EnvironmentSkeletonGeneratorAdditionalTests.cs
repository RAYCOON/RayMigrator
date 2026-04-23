// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Additional EnvironmentSkeletonGenerator tests for alias sanitization and output format.
/// </summary>
public class EnvironmentSkeletonGeneratorAdditionalTests
{
    [Fact]
    public void Generate_AliasWithHyphen_SanitizesToUnderscore()
    {
        var model = new ConfigurationModel();
        model.Products.Add(new ProductModel
        {
            Alias = "My-App",
            MigrationFilesRootDirectory = "./Migrations",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Back-End",
                    DatabaseType = "SqlServer",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "Main-DB", ConnectionString = "" }
                    }
                }
            }
        });

        var json = EnvironmentSkeletonGenerator.Generate(model);
        json.Should().Contain("MY_APP_BACK_END_MAIN_DB_CONNECTION_STRING");
    }

    [Fact]
    public void Generate_AliasWithDot_SanitizesToUnderscore()
    {
        var model = new ConfigurationModel();
        model.Products.Add(new ProductModel
        {
            Alias = "My.App",
            MigrationFilesRootDirectory = "./Migrations",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = "" }
                    }
                }
            }
        });

        var json = EnvironmentSkeletonGenerator.Generate(model);
        json.Should().Contain("MY_APP_BACKEND_MAINDB_CONNECTION_STRING");
    }

    [Fact]
    public void Generate_NonIndented_ProducesCompactJson()
    {
        var model = TestModelFactory.CreateValidModel();
        var json = EnvironmentSkeletonGenerator.Generate(model, indented: false);

        json.Should().NotContain("\n  ");
    }

    [Fact]
    public void Generate_Indented_ProducesFormattedJson()
    {
        var model = TestModelFactory.CreateValidModel();
        var json = EnvironmentSkeletonGenerator.Generate(model, indented: true);

        json.Should().Contain("\n");
    }

    [Fact]
    public void Generate_MultipleTargets_GeneratesOneEntryPerTarget()
    {
        var model = new ConfigurationModel();
        model.Products.Add(new ProductModel
        {
            Alias = "App",
            MigrationFilesRootDirectory = "./Migrations",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "Primary", ConnectionString = "" },
                        new() { Alias = "Replica", ConnectionString = "" },
                    }
                }
            }
        });

        var json = EnvironmentSkeletonGenerator.Generate(model);
        json.Should().Contain("APP_BACKEND_PRIMARY_CONNECTION_STRING");
        json.Should().Contain("APP_BACKEND_REPLICA_CONNECTION_STRING");
    }

    [Fact]
    public void Generate_NoDatabaseLogging_OmitsDatabaseLoggingSection()
    {
        var model = TestModelFactory.CreateValidModel();
        model.DatabaseLogging = null;

        var json = EnvironmentSkeletonGenerator.Generate(model);
        json.Should().NotContain("DatabaseLogging");
    }
}
