namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

/// <summary>
/// Creates valid test models for unit tests.
/// </summary>
internal static class TestModelFactory
{
    public static ConfigurationModel CreateValidModel()
    {
        return new ConfigurationModel
        {
            Repository = new RepositoryModel
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=localhost;Database=RayMigrator;User Id=sa;Password=secret;TrustServerCertificate=true",
                SchemaName = "migrations",
            },
            ProductDefaults = new ProductDefaultsModel(),
            Products = new List<ProductModel>
            {
                CreateValidProduct("MyProduct")
            },
            Serilog = new SerilogModel
            {
                MinimumLevelDefault = "Information",
                WriteTo = new List<SerilogSinkModel> { new() { Name = "Console" } }
            }
        };
    }

    public static ProductModel CreateValidProduct(string alias = "MyProduct")
    {
        return new ProductModel
        {
            Alias = alias,
            MigrationFilesRootDirectory = "./Migrations/" + alias,
            TargetGroups = new List<TargetGroupModel>
            {
                CreateValidTargetGroup("Backend")
            }
        };
    }

    public static TargetGroupModel CreateValidTargetGroup(string alias = "Backend", string dbType = "SqlServer")
    {
        return new TargetGroupModel
        {
            Alias = alias,
            DatabaseType = dbType,
            Targets = new List<TargetModel>
            {
                CreateValidTarget("MainDB")
            }
        };
    }

    public static TargetModel CreateValidTarget(string alias = "MainDB")
    {
        return new TargetModel
        {
            Alias = alias,
            ConnectionString = "Server=localhost;Database=MyApp;User Id=sa;Password=secret",
        };
    }

    public static CliToolModel CreateValidCliTool(string alias = "sqlcmd")
    {
        return new CliToolModel
        {
            Alias = alias,
            ExecutablePath = "sqlcmd",
            ArgumentTemplate = "-S {Server} -d {Database} -i {FilePath} -b",
            InputMode = "File",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 120,
        };
    }

    public static WizardSetupAnswers CreateValidSetupAnswers()
    {
        return new WizardSetupAnswers
        {
            RepositoryDatabaseType = "SqlServer",
            UseDatabaseLogging = true,
            UseCliTools = false,
            Products = new List<ProductSetup>
            {
                new()
                {
                    Alias = "MyApp",
                    Environments = new List<string> { "Development", "Docker" },
                    TargetGroups = new List<TargetGroupSetup>
                    {
                        new()
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            TargetAliases = new List<string> { "MainDB" }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a minimal valid JSON string for testing serialization.
    /// </summary>
    public static string CreateValidJson()
    {
        return """
        {
          "RayMigrator": {
            "Repository": {
              "DatabaseType": "SqlServer",
              "ConnectionString": "Server=localhost;Database=RayMigrator",
              "SchemaName": "migrations"
            },
            "ProductDefaults": {
              "MigrationErrorAction": "Terminate",
              "TargetGroupDefaults": {
                "TargetMigrationOrder": "Successively",
                "HashValidationScope": "File",
                "TargetDefaults": {
                  "DbCommandTimeoutInSeconds": 20
                }
              }
            },
            "Products": [{
              "Alias": "MyApp",
              "MigrationFilesRootDirectory": "./Migrations/MyApp",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [{
                  "Alias": "MainDB",
                  "ConnectionString": "Server=localhost;Database=MyApp"
                }]
              }]
            }],
            "Serilog": {
              "MinimumLevel": { "Default": "Information" },
              "WriteTo": [{ "Name": "Console" }]
            }
          }
        }
        """;
    }
}
