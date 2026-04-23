// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Validation;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// End-to-end test that replicates the user's exact scenario:
/// import three appsettings files, navigate to Overview, expect RULE_3_8 error.
/// </summary>
public class EndToEndCliToolValidationTests
{
    private const string BaseJson = """
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
          "MigrationErrorAction": "Terminate",
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
                  {
                    "Alias": "BackendDB",
                    "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING}"
                  }
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

    private const string DevelopmentJson = """
    {
      "RayMigrator": {
        "Repository": {
          "ConnectionString": "{ENV:REPO_CONNECTION_STRING_DEVELOPMENT}"
        }
      }
    }
    """;

    private const string MyAppDevelopmentJson = """
    {
      "RayMigrator": {
        "DatabaseLogging": {
          "ConnectionString": "{ENV:DBLOG_CONNECTION_STRING_DEVELOPMENT}"
        },
        "Products": [
          {
            "Alias": "MyApp",
            "TargetGroups": [
              {
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [
                  {
                    "Alias": "BackendDB",
                    "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_DEVELOPMENT}",
                    "UseCliToolAlias": "sqlcmd"
                  }
                ]
              }
            ]
          }
        ],
        "CliTools": [
          {
            "Alias": "sqlcmd",
            "ExecutablePath": "sqlcmd",
            "ArgumentTemplate": "-S {Server} -U {User} -P {Password} -d {Database} -i \"{FilePath}\" -b",
            "InputMode": "File",
            "CliToolTimeoutInSeconds": 120,
            "SuccessExitCodes": ["0"]
          }
        ]
      }
    }
    """;

    [Fact]
    public void UserScenario_ImportAndValidate_FiresRule38ForMissingCliToolParameters()
    {
        var svc = new WizardStateService();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = BaseJson,
            ["appsettings.development.json"] = DevelopmentJson,
            ["appsettings.myapp.development.json"] = MyAppDevelopmentJson,
        };

        svc.ImportFiles(files);
        var result = svc.ValidateAll();

        var rule38 = result.Errors.FirstOrDefault(e => e.Code == RuleIds.RULE_3_8);
        rule38.Should().NotBeNull(
            "RULE_3_8 must fire: Target has UseCliToolAlias=sqlcmd but no CliToolParameters anywhere. " +
            "Tool template has {Server}, {User}, {Password}, {Database} placeholders. " +
            $"Actual errors: {string.Join(" | ", result.Errors.Select(e => $"{e.Code}@{e.Path}: {e.Message}"))} | " +
            $"Actual warnings: {string.Join(" | ", result.Warnings.Select(e => $"{e.Code}@{e.Path}: {e.Message}"))}");
    }

    [Fact]
    public void UserScenario_ImportAndValidate_NoRule38_WhenCliToolParametersPresent()
    {
        // Positive pendant: import with the CliToolParameters present → validation must pass.
        // Regression guard against RULE_3_8 firing when the user's config is actually correct.
        const string MyAppDevWithParams = """
        {
          "RayMigrator": {
            "DatabaseLogging": {
              "ConnectionString": "{ENV:DBLOG_CONNECTION_STRING_DEVELOPMENT}"
            },
            "Products": [{
              "Alias": "MyApp",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [{
                  "Alias": "BackendDB",
                  "ConnectionString": "{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_DEVELOPMENT}",
                  "UseCliToolAlias": "sqlcmd",
                  "CliToolParameters": {
                    "Server": "localhost",
                    "User": "sa",
                    "Password": "secret",
                    "Database": "MyApp"
                  }
                }]
              }]
            }],
            "CliTools": [{
              "Alias": "sqlcmd",
              "ExecutablePath": "sqlcmd",
              "ArgumentTemplate": "-S {Server} -U {User} -P {Password} -d {Database} -i \"{FilePath}\" -b",
              "InputMode": "File",
              "CliToolTimeoutInSeconds": 120,
              "SuccessExitCodes": ["0"]
            }]
          }
        }
        """;

        var svc = new WizardStateService();
        svc.ImportFiles(new Dictionary<string, string>
        {
            ["appsettings.json"] = BaseJson,
            ["appsettings.development.json"] = DevelopmentJson,
            ["appsettings.myapp.development.json"] = MyAppDevWithParams,
        });
        var result = svc.ValidateAll();

        result.Errors.Should().NotContain(e => e.Code == RuleIds.RULE_3_8,
            "all required params are present — RULE_3_8 must not fire. " +
            $"Actual errors: {string.Join(" | ", result.Errors.Select(e => $"{e.Code}@{e.Path}"))}");
    }
}
