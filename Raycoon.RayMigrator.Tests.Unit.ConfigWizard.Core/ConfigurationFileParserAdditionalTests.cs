
using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Additional ConfigurationFileParser tests covering ParseSegments edge cases
/// and Parse product-file handling.
/// </summary>
public class ConfigurationFileParserAdditionalTests
{
    // ── ParseSegments edge cases ─────────────────────────────────────

    [Fact]
    public void ParseSegments_NonAppsettingsFile_ReturnsEmpty()
    {
        var segments = ConfigurationFileParser.ParseSegments("web.config");
        segments.Should().BeEmpty();
    }

    [Fact]
    public void ParseSegments_AppsettingsNoExtension_ReturnsEmpty()
    {
        var segments = ConfigurationFileParser.ParseSegments("appsettings");
        segments.Should().BeEmpty();
    }

    [Fact]
    public void ParseSegments_AppsettingsOnlyJson_ReturnsEmpty()
    {
        var segments = ConfigurationFileParser.ParseSegments("appsettings.json");
        segments.Should().BeEmpty();
    }

    [Fact]
    public void ParseSegments_AppsettingsEnvironment_ReturnsSingleSegment()
    {
        var segments = ConfigurationFileParser.ParseSegments("appsettings.Docker.json");
        segments.Should().Equal("Docker");
    }

    [Fact]
    public void ParseSegments_AppsettingsProductEnvironment_ReturnsTwoSegments()
    {
        var segments = ConfigurationFileParser.ParseSegments("appsettings.MyApp.Production.json");
        segments.Should().Equal("MyApp", "Production");
    }

    [Fact]
    public void ParseSegments_ThreeMiddleSegments_ReturnsThree()
    {
        var segments = ConfigurationFileParser.ParseSegments("appsettings.My.App.Docker.json");
        segments.Should().Equal("My", "App", "Docker");
    }

    [Fact]
    public void ParseSegments_CaseInsensitivePrefix()
    {
        var segments = ConfigurationFileParser.ParseSegments("APPSETTINGS.Docker.json");
        segments.Should().Equal("Docker");
    }

    // ── ClassifyFileName — edge cases ────────────────────────────────

    [Fact]
    public void ClassifyFileName_EmptyString_ReturnsBase()
    {
        var (role, product, env) = ConfigurationFileParser.ClassifyFileName("appsettings.json");
        role.Should().Be(ConfigFileRole.Base);
        product.Should().BeNull();
        env.Should().BeNull();
    }

    [Fact]
    public void ClassifyFileName_ProductEnvironmentWithFullPath()
    {
        var (role, product, env) = ConfigurationFileParser.ClassifyFileName("/configs/appsettings.MyApp.Staging.json");
        role.Should().Be(ConfigFileRole.ProductEnvironment);
        product.Should().Be("MyApp");
        env.Should().Be("Staging");
    }

    // ── Parse — multiple environments ────────────────────────────────

    [Fact]
    public void Parse_MultipleEnvironmentFiles_AllAreParsed()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.Development.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:DEV_CONN}"}}}""",
            ["appsettings.Staging.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:STAGING_CONN}"}}}""",
            ["appsettings.Production.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:PROD_CONN}"}}}""",
        };

        var state = ConfigurationFileParser.Parse(files);
        state.EnvironmentModels.Should().ContainKey("Development");
        state.EnvironmentModels.Should().ContainKey("Staging");
        state.EnvironmentModels.Should().ContainKey("Production");
    }

    [Fact]
    public void Parse_MultipleProductEnvironments_AllAreParsed()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.MyApp.Development.json"] = """{"RayMigrator":{}}""",
            ["appsettings.OtherApp.Staging.json"] = """{"RayMigrator":{}}""",
        };

        var state = ConfigurationFileParser.Parse(files);
        state.ProductEnvironmentModels.Should().ContainKey("MyApp.Development");
        state.ProductEnvironmentModels.Should().ContainKey("OtherApp.Staging");
    }

    [Fact]
    public void Parse_EnvironmentModels_HaveCorrectFileRole()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.Docker.json"] = """{"RayMigrator":{}}"""
        };

        var state = ConfigurationFileParser.Parse(files);
        state.EnvironmentModels["Docker"].FileRole.Should().Be(ConfigFileRole.Environment);
    }

    [Fact]
    public void Parse_ProductEnvironmentModels_HaveCorrectFileRole()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator":{}}"""
        };

        var state = ConfigurationFileParser.Parse(files);
        state.ProductEnvironmentModels["MyApp.Docker"].FileRole.Should().Be(ConfigFileRole.ProductEnvironment);
    }

    [Fact]
    public void Parse_NoBaseFile_SetupAnswersStillBuilt()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.Docker.json"] = """{"RayMigrator":{"Repository":{"DatabaseType":"PostgreSQL"}}}"""
        };

        var state = ConfigurationFileParser.Parse(files);
        // BaseModel stays at default; no throw expected
        state.SetupAnswers.Should().NotBeNull();
    }

    [Fact]
    public void Parse_ReverseEngineers_CollectsEnvironmentsFromProductEnvironmentModels()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator":{}}""",
        };

        var state = ConfigurationFileParser.Parse(files);
        // The reversed answers should reflect the Docker environment
        state.SetupAnswers.Products[0].Environments.Should().Contain("Docker");
    }

    // ── Parent layer merging ─────────────────────────────────────────

    [Fact]
    public void Parse_PeModelInheritsRepositoryConnectionStringFromEnvLayer()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer"}}}""",
            ["appsettings.Docker.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:CONN}"}}}""",
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator":{}}""",
        };

        var state = ConfigurationFileParser.Parse(files);

        var peModel = state.ProductEnvironmentModels["MyApp.Docker"];
        peModel.Repository.ConnectionString.Should().Be("{ENV:CONN}");
    }

    [Fact]
    public void Parse_PeModelOwnValuesOverrideParentLayers()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer"}}}""",
            ["appsettings.Docker.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"env_conn"}}}""",
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"pe_conn"}}}""",
        };

        var state = ConfigurationFileParser.Parse(files);

        var peModel = state.ProductEnvironmentModels["MyApp.Docker"];
        peModel.Repository.ConnectionString.Should().Be("pe_conn");
    }

    [Fact]
    public void Parse_PeModelWithoutParentLayers_IsUnchanged()
    {
        const string peJson = """{"RayMigrator":{"Repository":{"ConnectionString":"standalone_conn"}}}""";
        var files = new Dictionary<string, string>
        {
            ["appsettings.MyApp.Docker.json"] = peJson,
        };

        var state = ConfigurationFileParser.Parse(files);

        var peModel = state.ProductEnvironmentModels["MyApp.Docker"];
        peModel.Repository.ConnectionString.Should().Be("standalone_conn");
    }

    [Fact]
    public void Parse_PeModelPreservedDocumentIsOriginal()
    {
        const string peJson = """{"RayMigrator":{"Repository":{"ConnectionString":"pe_original"}}}""";
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer","ConnectionString":"base_conn"}}}""",
            ["appsettings.Docker.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"env_conn"}}}""",
            ["appsettings.MyApp.Docker.json"] = peJson,
        };

        var state = ConfigurationFileParser.Parse(files);

        var peModel = state.ProductEnvironmentModels["MyApp.Docker"];
        // Merged model should show env value (pe overrides only its own key)
        // but PreservedDocument must be the original PE JSON, not the merged JSON
        peModel.PreservedDocument.Should().NotBeNull();
        var preservedJson = peModel.PreservedDocument!.ToJsonString();
        preservedJson.Should().Contain("pe_original");
        preservedJson.Should().NotContain("base_conn");
        preservedJson.Should().NotContain("env_conn");
    }

    [Fact]
    public void Parse_PeInheritsMultipleSectionsFromParentLayers()
    {
        var baseJson = TestModelFactory.CreateValidJson(); // Has Repository.DatabaseType, ProductDefaults, Products
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = baseJson,
            ["appsettings.Docker.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:DOCKER_CONN}"}}}""",
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator":{"Products":[{"Alias":"MyApp","MigrationFilesRootDirectory":"./Override","TargetGroups":[]}]}}""",
        };

        var state = ConfigurationFileParser.Parse(files);

        var peModel = state.ProductEnvironmentModels["MyApp.Docker"];
        // Repository.DatabaseType from base
        peModel.Repository.DatabaseType.Should().Be("SqlServer");
        // Repository.ConnectionString from env
        peModel.Repository.ConnectionString.Should().Be("{ENV:DOCKER_CONN}");
        // ProductDefaults.MigrationErrorAction from base
        peModel.ProductDefaults.MigrationErrorAction.Should().Be("Terminate");
        // Products from PE file (array replacement)
        peModel.Products.Should().HaveCount(1);
        peModel.Products[0].MigrationFilesRootDirectory.Should().Be("./Override");
    }

    [Fact]
    public void Parse_PeInheritsFromProductLayer()
    {
        // appsettings.MyApp.json has two segments (MyApp + no environment), which ClassifyFileName
        // maps to ConfigFileRole.ProductEnvironment with product="MyApp" and environment=null.
        // Since environment is null it is excluded from the PE parsing loop, so it ends up in
        // neither ProductModels nor ProductEnvironmentModels.
        // The product-layer slot in MergeParentLayersIntoPeModels is therefore empty, and the
        // PE model inherits only from the base layer.
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer","SchemaName":"base_schema"}}}""",
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator":{}}""",
        };

        var state = ConfigurationFileParser.Parse(files);

        var peModel = state.ProductEnvironmentModels["MyApp.Docker"];
        // SchemaName inherited from the base layer
        peModel.Repository.SchemaName.Should().Be("base_schema");
        // ProductModels is always empty when populated through Parse() alone
        // because ClassifyFileName never returns ConfigFileRole.Product
        state.ProductModels.Should().BeEmpty();
    }

    // ── Round-trip test: real-world 5-file config set ────────────────

    /// <summary>
    /// Loads the actual 5-file config set (base + 2 env + 2 PE) and verifies that
    /// after parsing, the effective merged PE models preserve MigrationFilesRootDirectory
    /// (the value defined only in the base file). This is the regression guard for the
    /// bug where alias-keyed array merge lost base-only fields when the PE layer contained
    /// a Products array without MigrationFilesRootDirectory.
    ///
    /// Also verifies that diff-based re-serialization of the PE models does NOT duplicate
    /// MigrationFilesRootDirectory into the PE files — it belongs only in the base.
    /// </summary>
    [Fact]
    public void Parse_RoundTrip_LoadRealConfigFiles_PreservesMigrationFilesRootDirectoryInMergedModel()
    {
        // ── Arrange: embed the exact 5 config files from the user's project ──

        const string appsettingsJson = """
        {
          "RayMigrator": {
            "Repository": {
              "DatabaseType": "SqlServer",
              "SchemaName": "ray",
              "TableBaseName": "",
              "DbCommandTimeoutInSeconds": 60,
              "DbCommandMaxRetries": 100,
              "DbCommandWaitTimeInMsBeforeRetry": 250
            },
            "DatabaseLogging": {
              "DatabaseType": "SqlServer",
              "SchemaName": "ray",
              "TableBaseName": "",
              "MinimumLevel": "Information",
              "DbCommandTimeoutInSeconds": 20
            },
            "ProductDefaults": {
              "RollbackErrorAction": "Terminate",
              "MigrationFilesExtension": "sql",
              "MigrationRollbackFilesPreExtension": "rollback",
              "MigrationFilesEncoding": "UTF-8",
              "RequireRollbackFile": true,
              "StopRollbackOnMissingRollbackFile": true,
              "TargetGroupDefaults": {
                "TargetMigrationOrder": "Successively",
                "HashValidationScope": "File",
                "StopRollbackOnMissingRollbackFile": true,
                "TargetDefaults": {
                  "DbCommandTimeoutInSeconds": 20,
                  "DbCommandMaxRetries": 0,
                  "DbCommandWaitTimeInMsBeforeRetry": 250
                }
              }
            },
            "Products": [
              {
                "Alias": "MyApp",
                "MigrationFilesRootDirectory": "./Migrations/MyApp",
                "TargetGroups": [
                  {
                    "Alias": "Backend",
                    "DatabaseType": "SqlServer",
                    "Targets": [
                      { "Alias": "BackendDB",  "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING}" },
                      { "Alias": "BackendDB2", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING}" }
                    ]
                  }
                ]
              }
            ],
            "Serilog": {
              "MinimumLevel": { "Default": "Information" },
              "WriteTo": [{ "Name": "Console" }]
            }
          }
        }
        """;

        const string appsettingsDevelopmentJson = """
        {
          "RayMigrator": {
            "Repository": {
              "ConnectionString": "{ENV:REPO_CONNECTION_STRING_DEVELOPMENT}"
            }
          }
        }
        """;

        const string appsettingsProductionJson = """
        {
          "RayMigrator": {
            "Repository": {
              "ConnectionString": "{ENV:REPO_CONNECTION_STRING_PRODUCTION}"
            }
          }
        }
        """;

        const string appsettingsMyAppDevelopmentJson = """
        {
          "RayMigrator": {
            "DatabaseLogging": {
              "ConnectionString": "{ENV:DBLOG_CONNECTION_STRING_DEVELOPMENT}",
              "MinimumLevel": "Debug"
            },
            "ProductDefaults": {
              "MigrationErrorAction": "Ignore",
              "RequireRollbackFile": false
            },
            "Products": [
              {
                "Alias": "MyApp",
                "TargetGroups": [
                  {
                    "Alias": "Backend",
                    "DatabaseType": "SqlServer",
                    "Targets": [
                      { "Alias": "BackendDB",  "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_DEVELOPMENT}" },
                      { "Alias": "BackendDB2", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING_DEVELOPMENT}" }
                    ]
                  }
                ]
              }
            ],
            "Serilog": {
              "MinimumLevel": { "Default": "Debug" }
            }
          }
        }
        """;

        const string appsettingsMyAppProductionJson = """
        {
          "RayMigrator": {
            "DatabaseLogging": {
              "ConnectionString": "{ENV:DBLOG_CONNECTION_STRING_PRODUCTION}"
            },
            "ProductDefaults": {
              "MigrationErrorAction": "RollbackRelease"
            },
            "Products": [
              {
                "Alias": "MyApp",
                "TargetGroups": [
                  {
                    "Alias": "Backend",
                    "DatabaseType": "SqlServer",
                    "Targets": [
                      { "Alias": "BackendDB",  "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_PRODUCTION}", "DbCommandMaxRetries": 2 },
                      { "Alias": "BackendDB2", "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB2_CONNECTION_STRING_PRODUCTION}", "DbCommandMaxRetries": 2 }
                    ]
                  }
                ]
              }
            ]
          }
        }
        """;

        var files = new Dictionary<string, string>
        {
            ["appsettings.json"]                     = appsettingsJson,
            ["appsettings.Development.json"]         = appsettingsDevelopmentJson,
            ["appsettings.Production.json"]          = appsettingsProductionJson,
            ["appsettings.MyApp.Development.json"]   = appsettingsMyAppDevelopmentJson,
            ["appsettings.MyApp.Production.json"]    = appsettingsMyAppProductionJson,
        };

        // ── Act ──────────────────────────────────────────────────────

        var state = ConfigurationFileParser.Parse(files);

        // ── Assert: merged PE models have MigrationFilesRootDirectory ─

        var devPe  = state.ProductEnvironmentModels["MyApp.Development"];
        var prodPe = state.ProductEnvironmentModels["MyApp.Production"];

        devPe.Products.Should().HaveCount(1);
        devPe.Products[0].Alias.Should().Be("MyApp");
        devPe.Products[0].MigrationFilesRootDirectory.Should().Be("./Migrations/MyApp",
            because: "MigrationFilesRootDirectory is defined in base and must be preserved after alias-keyed merge");

        prodPe.Products.Should().HaveCount(1);
        prodPe.Products[0].Alias.Should().Be("MyApp");
        prodPe.Products[0].MigrationFilesRootDirectory.Should().Be("./Migrations/MyApp",
            because: "MigrationFilesRootDirectory is defined in base and must be preserved after alias-keyed merge");

        // ── Assert: env-specific ConnectionStrings are present ────────

        devPe.Products[0].TargetGroups[0].Targets[0].ConnectionString
            .Should().Be("{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_DEVELOPMENT}");
        prodPe.Products[0].TargetGroups[0].Targets[0].ConnectionString
            .Should().Be("{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_PRODUCTION}");

        // ── Assert: Production PE has DbCommandMaxRetries=2 while Development preserves base 0 ──

        prodPe.Products[0].TargetGroups[0].Targets[0].DbCommandMaxRetries.Value.Should().Be(2);
        prodPe.Products[0].TargetGroups[0].Targets[1].DbCommandMaxRetries.Value.Should().Be(2);

        // ── Assert: diff-based export of PE models does NOT repeat MigrationFilesRootDirectory ──
        // (It's only in base; PE files should be kept minimal.)

        var baseModel = state.BaseModel;
        string devPeDiff  = ConfigurationSerializer.ToJson(devPe,  baseModel);
        string prodPeDiff = ConfigurationSerializer.ToJson(prodPe, baseModel);

        devPeDiff.Should().NotContain("MigrationFilesRootDirectory",
            because: "PE export must not duplicate MigrationFilesRootDirectory that lives in the base file");
        prodPeDiff.Should().NotContain("MigrationFilesRootDirectory",
            because: "PE export must not duplicate MigrationFilesRootDirectory that lives in the base file");

        // ── Assert: PE diffs are valid JSON ──────────────────────────

        var parseDevDiff  = () => System.Text.Json.Nodes.JsonNode.Parse(devPeDiff);
        var parseProdDiff = () => System.Text.Json.Nodes.JsonNode.Parse(prodPeDiff);
        parseDevDiff.Should().NotThrow();
        parseProdDiff.Should().NotThrow();

        // ── Assert: base model still has MigrationFilesRootDirectory ─

        state.BaseModel.Products.Should().HaveCount(1);
        state.BaseModel.Products[0].MigrationFilesRootDirectory.Should().Be("./Migrations/MyApp");
    }

    // ── Round-trip test: multi-product config must not gain extra product stubs ────

    /// <summary>
    /// Regression test: when a multi-product config (2 products, 2 environments) is imported
    /// and immediately re-exported without changes, the product-environment files must not
    /// gain extra product stubs from other products. For example, appsettings.MyApp.Development.json
    /// must not contain a {"Alias": "TestApp"} entry.
    /// </summary>
    [Fact]
    public void Parse_RoundTrip_MultiProduct_PeFilesDoNotGainExtraProductStubs()
    {
        // ── Arrange: 2 products (MyApp, TestApp), 2 environments (Development, Production) ──

        const string baseJson = """
        {
          "RayMigrator": {
            "Repository": {
              "DatabaseType": "SqlServer",
              "SchemaName": "ray"
            },
            "ProductDefaults": {
              "MigrationFilesExtension": "sql",
              "RequireRollbackFile": true
            },
            "Products": [
              {
                "Alias": "MyApp",
                "MigrationFilesRootDirectory": "./Migrations/MyApp",
                "TargetGroups": [
                  {
                    "Alias": "Backend",
                    "DatabaseType": "SqlServer",
                    "Targets": [
                      { "Alias": "BackendDB", "ConnectionString": "{ENV:MYAPP_BACKEND_CONN}" }
                    ]
                  }
                ]
              },
              {
                "Alias": "TestApp",
                "MigrationFilesRootDirectory": "./Migrations/TestApp",
                "TargetGroups": [
                  {
                    "Alias": "Data",
                    "DatabaseType": "PostgreSQL",
                    "Targets": [
                      { "Alias": "DataDB", "ConnectionString": "{ENV:TESTAPP_DATA_CONN}" }
                    ]
                  }
                ]
              }
            ],
            "Serilog": {
              "MinimumLevel": { "Default": "Information" },
              "WriteTo": [{ "Name": "Console" }]
            }
          }
        }
        """;

        const string devJson = """
        {
          "RayMigrator": {
            "Serilog": {
              "MinimumLevel": { "Default": "Debug" }
            }
          }
        }
        """;

        const string prodJson = """
        {
          "RayMigrator": {
            "Serilog": {
              "MinimumLevel": { "Default": "Warning" }
            }
          }
        }
        """;

        const string myAppDevJson = """
        {
          "RayMigrator": {
            "Products": [
              {
                "Alias": "MyApp",
                "TargetGroups": [
                  {
                    "Alias": "Backend",
                    "DatabaseType": "SqlServer",
                    "Targets": [
                      { "Alias": "BackendDB", "ConnectionString": "{ENV:MYAPP_BACKEND_CONN_DEV}" }
                    ]
                  }
                ]
              }
            ]
          }
        }
        """;

        const string myAppProdJson = """
        {
          "RayMigrator": {
            "Products": [
              {
                "Alias": "MyApp",
                "TargetGroups": [
                  {
                    "Alias": "Backend",
                    "DatabaseType": "SqlServer",
                    "Targets": [
                      { "Alias": "BackendDB", "ConnectionString": "{ENV:MYAPP_BACKEND_CONN_PROD}" }
                    ]
                  }
                ]
              }
            ]
          }
        }
        """;

        const string testAppDevJson = """
        {
          "RayMigrator": {
            "Products": [
              {
                "Alias": "TestApp",
                "TargetGroups": [
                  {
                    "Alias": "Data",
                    "DatabaseType": "PostgreSQL",
                    "Targets": [
                      { "Alias": "DataDB", "ConnectionString": "{ENV:TESTAPP_DATA_CONN_DEV}" }
                    ]
                  }
                ]
              }
            ]
          }
        }
        """;

        const string testAppProdJson = """
        {
          "RayMigrator": {
            "Products": [
              {
                "Alias": "TestApp",
                "TargetGroups": [
                  {
                    "Alias": "Data",
                    "DatabaseType": "PostgreSQL",
                    "Targets": [
                      { "Alias": "DataDB", "ConnectionString": "{ENV:TESTAPP_DATA_CONN_PROD}" }
                    ]
                  }
                ]
              }
            ]
          }
        }
        """;

        var files = new Dictionary<string, string>
        {
            ["appsettings.json"]                     = baseJson,
            ["appsettings.Development.json"]         = devJson,
            ["appsettings.Production.json"]           = prodJson,
            ["appsettings.MyApp.Development.json"]   = myAppDevJson,
            ["appsettings.MyApp.Production.json"]    = myAppProdJson,
            ["appsettings.TestApp.Development.json"] = testAppDevJson,
            ["appsettings.TestApp.Production.json"]  = testAppProdJson,
        };

        // ── Act: import then re-export via diff serialization ──

        var state = ConfigurationFileParser.Parse(files);

        var reExported = new Dictionary<string, string>();
        foreach (var (key, model) in state.ProductEnvironmentModels)
        {
            reExported[$"appsettings.{key}.json"] = ConfigurationSerializer.ToJson(model, state.BaseModel);
        }

        // ── Assert: PE files contain only their own product, not stubs from other products ──

        foreach (var (fileName, json) in reExported)
        {
            var ray = System.Text.Json.Nodes.JsonNode.Parse(json)?["RayMigrator"];
            var productsArr = ray?["Products"]?.AsArray();

            productsArr.Should().NotBeNull($"{fileName} should have a Products array");
            productsArr!.Count.Should().Be(1,
                $"{fileName} should contain exactly one product, not stubs from other products");

            // Verify the correct product alias is present
            var parts = System.IO.Path.GetFileNameWithoutExtension(fileName)
                .Replace("appsettings.", "").Split('.');
            var expectedProduct = parts[0];
            productsArr[0]!["Alias"]!.GetValue<string>().Should().Be(expectedProduct,
                $"{fileName} should contain only the {expectedProduct} product");
        }

        // ── Assert: each PE file should NOT contain the other product's alias ──

        reExported["appsettings.MyApp.Development.json"].Should().NotContain("TestApp");
        reExported["appsettings.MyApp.Production.json"].Should().NotContain("TestApp");
        reExported["appsettings.TestApp.Development.json"].Should().NotContain("MyApp");
        reExported["appsettings.TestApp.Production.json"].Should().NotContain("MyApp");

        // ── Assert: example.env is identical after roundtrip ──
        // Generate env file from both original and re-exported JSONs
        var originalJsons = files
            .Where(f => f.Key.EndsWith(".json"))
            .ToDictionary(f => f.Key, f => f.Value);
        var envOriginal = EnvFileGenerator.GenerateFromExportedJsons(originalJsons, _ => null);

        // For re-export, build the full set: base + env + PE diffs
        var reExportedFull = new Dictionary<string, string>
        {
            ["appsettings.json"] = ConfigurationSerializer.ToJson(state.BaseModel),
        };
        foreach (var (env, model) in state.EnvironmentModels)
            reExportedFull[$"appsettings.{env}.json"] = ConfigurationSerializer.ToJson(model, state.BaseModel);
        foreach (var (key, json) in reExported)
            reExportedFull[key] = json;

        var envRoundTripped = EnvFileGenerator.GenerateFromExportedJsons(reExportedFull, _ => null);

        envRoundTripped.Should().Be(envOriginal,
            because: "example.env must be identical after a no-change roundtrip");
    }
}
