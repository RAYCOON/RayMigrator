// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Additional EnvFileGenerator tests for DatabaseLogging, product MigrationFilesRootDirectory,
/// and comment/header content.
/// </summary>
public class EnvFileGeneratorAdditionalTests
{
    [Fact]
    public void Generate_DatabaseLoggingEnvVar_IsIncluded()
    {
        var model = new ConfigurationModel();
        model.Repository.ConnectionString = "Server=localhost";
        model.DatabaseLogging = new DatabaseLoggingModel
        {
            ConnectionString = "{ENV:DBLOG_CONN}"
        };

        var result = EnvFileGenerator.Generate(model, _ => null);
        result.Should().Contain("DBLOG_CONN");
        result.Should().Contain("DatabaseLogging.ConnectionString");
    }

    [Fact]
    public void Generate_ProductMigrationFilesRootDirectoryEnvVar_IsIncluded()
    {
        var model = new ConfigurationModel();
        model.Repository.ConnectionString = "Server=localhost";
        model.Products.Add(new ProductModel
        {
            Alias = "MyApp",
            MigrationFilesRootDirectory = "{ENV:MIGRATIONS_PATH}",
            TargetGroups = new List<TargetGroupModel>()
        });

        var result = EnvFileGenerator.Generate(model, _ => null);
        result.Should().Contain("MIGRATIONS_PATH");
        result.Should().Contain("MigrationFilesRootDirectory");
    }

    [Fact]
    public void Generate_WithEnvVars_ContainsHeader()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:REPO_CONN}";

        var result = EnvFileGenerator.Generate(model, _ => null);
        result.Should().Contain("RayMigrator Environment Variables");
    }

    [Fact]
    public void Generate_WithEnvVars_ContainsCopyInstruction()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:REPO_CONN}";

        var result = EnvFileGenerator.Generate(model, _ => null);
        result.Should().Contain(".env");
    }

    [Fact]
    public void Generate_EnvVarsOrderedAlphabetically()
    {
        var model = new ConfigurationModel();
        model.Repository.ConnectionString = "{ENV:ZZZ_CONN}";
        model.Products.Add(new ProductModel
        {
            Alias = "App",
            MigrationFilesRootDirectory = "{ENV:AAA_PATH}",
            TargetGroups = new List<TargetGroupModel>()
        });

        var result = EnvFileGenerator.Generate(model, _ => null);
        var aaaIndex = result.IndexOf("AAA_PATH", StringComparison.Ordinal);
        var zzzIndex = result.IndexOf("ZZZ_CONN", StringComparison.Ordinal);

        aaaIndex.Should().BeLessThan(zzzIndex);
    }

    [Fact]
    public void Generate_SameVarInMultiplePlaces_AppearsOnceAsAssignment()
    {
        var model = new ConfigurationModel();
        model.Repository.ConnectionString = "{ENV:SHARED_CONN}";
        model.DatabaseLogging = new DatabaseLoggingModel
        {
            ConnectionString = "{ENV:SHARED_CONN}"
        };

        var result = EnvFileGenerator.Generate(model, _ => null);
        var assignmentLines = result.Split('\n').Where(l => l.StartsWith("SHARED_CONN=")).ToList();
        assignmentLines.Should().HaveCount(1);
    }

    [Fact]
    public void Generate_DefaultResolver_UsesEnvironmentGetEnvironmentVariable()
    {
        // This just verifies no exception is thrown when using default resolver
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:NON_EXISTENT_VAR_XYZ_12345}";

        var act = () => EnvFileGenerator.Generate(model);
        act.Should().NotThrow();
    }

    // ── GenerateFromExportedJsons ────────────────────────────────────

    [Fact]
    public void GenerateFromExportedJsons_CollectsEnvVarsFromAllFiles()
    {
        var exportedJsons = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator":{"Products":[{"Alias":"MyApp","ConnectionString":"{ENV:CONN_A}"}]}}""",
            ["appsettings.Development.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:CONN_B}"}}}""",
            ["appsettings.MyApp.Development.json"] = """{"RayMigrator":{"DatabaseLogging":{"ConnectionString":"{ENV:CONN_C}"}}}""",
        };

        var result = EnvFileGenerator.GenerateFromExportedJsons(exportedJsons, _ => null);

        result.Should().Contain("CONN_A=");
        result.Should().Contain("CONN_B=");
        result.Should().Contain("CONN_C=");
    }

    [Fact]
    public void GenerateFromExportedJsons_NoEnvVars_ReturnsNoVarsMessage()
    {
        var exportedJsons = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer"}}}""",
        };

        var result = EnvFileGenerator.GenerateFromExportedJsons(exportedJsons, _ => null);

        result.Should().Contain("No environment variables");
    }

    [Fact]
    public void GenerateFromExportedJsons_IncludesFileNameInUsedInComment()
    {
        var exportedJsons = new Dictionary<string, string>
        {
            ["appsettings.Development.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:REPO_CONN}"}}}""",
        };

        var result = EnvFileGenerator.GenerateFromExportedJsons(exportedJsons, _ => null);

        result.Should().Contain("# Used in: appsettings.Development.json");
        result.Should().Contain("REPO_CONN=");
    }

    [Fact]
    public void GenerateFromExportedJsons_UsedInComment_FileNamesSortedAlphabetically()
    {
        // Same env var used in multiple files — "Used in" must list them in alphabetical order
        // regardless of the input dictionary iteration order.
        var exportedJsons = new Dictionary<string, string>
        {
            ["appsettings.MyApp.Prod.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:SHARED_CONN}"}}}""",
            ["appsettings.App2.Prod.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:SHARED_CONN}"}}}""",
            ["appsettings.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:SHARED_CONN}"}}}""",
        };

        var result = EnvFileGenerator.GenerateFromExportedJsons(exportedJsons, _ => null);

        // Files must be sorted: App2 < MyApp < appsettings (ordinal)
        result.Should().Contain("# Used in: appsettings.App2.Prod.json, appsettings.MyApp.Prod.json, appsettings.json");
    }

    [Fact]
    public void GenerateFromExportedJsons_RealConfigFiles_AllEnvVarsPresent()
    {
        // Simulate the exported JSON strings from the real 5-file config set
        var exportedJsons = new Dictionary<string, string>
        {
            ["appsettings.json"] = """
            {
              "RayMigrator": {
                "Products": [{
                  "Alias": "MyApp",
                  "MigrationFilesRootDirectory": "./Migrations/MyApp",
                  "TargetGroups": [{
                    "Alias": "Backend",
                    "DatabaseType": "SqlServer",
                    "Targets": [
                      { "Alias": "BackendDB", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING}" },
                      { "Alias": "BackendDB2", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING}" }
                    ]
                  }]
                }]
              }
            }
            """,
            ["appsettings.Development.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:REPO_CONNECTION_STRING_DEVELOPMENT}"}}}""",
            ["appsettings.Production.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:REPO_CONNECTION_STRING_PRODUCTION}"}}}""",
            ["appsettings.MyApp.Development.json"] = """
            {
              "RayMigrator": {
                "DatabaseLogging": { "ConnectionString": "{ENV:DBLOG_CONNECTION_STRING_DEVELOPMENT}" },
                "Products": [{
                  "Alias": "MyApp",
                  "TargetGroups": [{
                    "Alias": "Backend",
                    "Targets": [
                      { "Alias": "BackendDB", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_DEVELOPMENT}" },
                      { "Alias": "BackendDB2", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING_DEVELOPMENT}" }
                    ]
                  }]
                }]
              }
            }
            """,
            ["appsettings.MyApp.Production.json"] = """
            {
              "RayMigrator": {
                "DatabaseLogging": { "ConnectionString": "{ENV:DBLOG_CONNECTION_STRING_PRODUCTION}" },
                "Products": [{
                  "Alias": "MyApp",
                  "TargetGroups": [{
                    "Alias": "Backend",
                    "Targets": [
                      { "Alias": "BackendDB", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_PRODUCTION}" },
                      { "Alias": "BackendDB2", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING_PRODUCTION}" }
                    ]
                  }]
                }]
              }
            }
            """,
        };

        var result = EnvFileGenerator.GenerateFromExportedJsons(exportedJsons, _ => null);

        // All ENV vars from ALL files must appear
        result.Should().Contain("DBLOG_CONNECTION_STRING_DEVELOPMENT=");
        result.Should().Contain("DBLOG_CONNECTION_STRING_PRODUCTION=");
        result.Should().Contain("MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING=");
        result.Should().Contain("MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING=");
        result.Should().Contain("MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_DEVELOPMENT=");
        result.Should().Contain("MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_PRODUCTION=");
        result.Should().Contain("MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING_DEVELOPMENT=");
        result.Should().Contain("MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING_PRODUCTION=");
        result.Should().Contain("REPO_CONNECTION_STRING_DEVELOPMENT=");
        result.Should().Contain("REPO_CONNECTION_STRING_PRODUCTION=");
    }
}
