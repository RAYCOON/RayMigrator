// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Creates a pre-filled WizardState from WizardSetupAnswers.
/// </summary>
public static class ConfigurationScaffolder
{
    /// <summary>
    /// Creates a minimal WizardState with one product, one environment, one target group, and one target.
    /// Used by the Web wizard when the user clicks "Create New" (no interview needed).
    /// </summary>
    public static WizardState ScaffoldMinimal()
    {
        var answers = new WizardSetupAnswers
        {
            RepositoryDatabaseType = "SqlServer",
            UseDatabaseLogging = true,
            UseCliTools = false,
            Products = new List<ProductSetup>
            {
                new()
                {
                    Alias = "MyApp",
                    Environments = new List<string> { "Development" },
                    TargetGroups = new List<TargetGroupSetup>
                    {
                        new()
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            TargetAliases = new List<string> { "BackendDB" }
                        }
                    }
                }
            }
        };

        return Scaffold(answers);
    }

    /// <summary>
    /// Creates a WizardState from setup answers with sensible defaults.
    /// </summary>
    public static WizardState Scaffold(WizardSetupAnswers answers)
    {
        var model = new ConfigurationModel();

        // 1. Repository
        model.Repository = new RepositoryModel
        {
            DatabaseType = answers.RepositoryDatabaseType,
            ConnectionString = "{ENV:REPO_CONNECTION_STRING}",
            SchemaName = answers.RepositoryDatabaseType == "Sqlite" ? "" : "ray",
        };

        // 2. DatabaseLogging
        if (answers.UseDatabaseLogging)
        {
            model.DatabaseLogging = new DatabaseLoggingModel
            {
                DatabaseType = answers.RepositoryDatabaseType,
                ConnectionString = "{ENV:DBLOG_CONNECTION_STRING}",
                SchemaName = answers.RepositoryDatabaseType == "Sqlite" ? "" : "ray",
            };
        }

        // 3. ProductDefaults
        model.ProductDefaults = new ProductDefaultsModel
        {
            MigrationErrorAction = "Terminate",
            RollbackErrorAction = "Terminate",
            MigrationFilesExtension = "sql",
            MigrationRollbackFilesPreExtension = "rollback",
            MigrationFilesEncoding = "UTF-8",
            RequireRollbackFile = true,
            StopRollbackOnMissingRollbackFile = true,
            TargetGroupDefaults = new TargetGroupDefaultsModel
            {
                TargetMigrationOrder = "Successively",
                HashValidationScope = "File",
                StopRollbackOnMissingRollbackFile = true,
                TargetDefaults = new TargetDefaultsModel
                {
                    DbCommandTimeoutInSeconds = 20,
                    DbCommandMaxRetries = 0,
                    DbCommandWaitTimeInMsBeforeRetry = 250,
                }
            }
        };

        // Collect all unique database types across all target groups
        var allDbTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 4. Products
        foreach (var productSetup in answers.Products)
        {
            var product = new ProductModel
            {
                Alias = productSetup.Alias,
                MigrationFilesRootDirectory = $"./Migrations/{productSetup.Alias}",
            };

            foreach (var tgSetup in productSetup.TargetGroups)
            {
                allDbTypes.Add(tgSetup.DatabaseType);

                var targetGroup = new TargetGroupModel
                {
                    Alias = tgSetup.Alias,
                    DatabaseType = tgSetup.DatabaseType,
                };

                if (tgSetup.TargetAliases.Count == 0)
                {
                    // Default: one target
                    targetGroup.Targets.Add(new TargetModel
                    {
                        Alias = "MainDB",
                        ConnectionString = "{ENV:" +
                            $"{SanitizeForEnv(productSetup.Alias)}_{SanitizeForEnv(tgSetup.Alias)}_MAINDB_CONNECTION_STRING" + "}",
                    });
                }
                else
                {
                    foreach (var targetAlias in tgSetup.TargetAliases)
                    {
                        targetGroup.Targets.Add(new TargetModel
                        {
                            Alias = targetAlias,
                            ConnectionString = "{ENV:" +
                                $"{SanitizeForEnv(productSetup.Alias)}_{SanitizeForEnv(tgSetup.Alias)}_{SanitizeForEnv(targetAlias)}_CONNECTION_STRING" + "}",
                        });
                    }
                }

                product.TargetGroups.Add(targetGroup);
            }

            model.Products.Add(product);
        }

        // 5. CliTools
        if (answers.UseCliTools)
        {
            foreach (var dbType in allDbTypes)
            {
                var presets = CliToolPresetProvider.GetPresetsForDatabaseType(dbType);
                var nativePreset = presets.FirstOrDefault(p => !p.IsDockerVariant);
                if (nativePreset != null)
                {
                    model.CliTools.Add(new CliToolModel
                    {
                        Alias = nativePreset.Alias,
                        ExecutablePath = nativePreset.ExecutablePath,
                        ArgumentTemplate = nativePreset.ArgumentTemplate,
                        InputMode = nativePreset.InputMode,
                        SuccessExitCodes = new List<string>(nativePreset.SuccessExitCodes),
                        CliToolTimeoutInSeconds = nativePreset.CliToolTimeoutInSeconds,
                    });
                }
            }

            // If all target groups share the same database type, set ProductDefaults.UseCliToolAlias
            if (allDbTypes.Count == 1 && model.CliTools.Count == 1)
            {
                model.ProductDefaults.UseCliToolAlias = model.CliTools[0].Alias;
            }
        }

        // 6. Serilog
        model.Serilog = new SerilogModel
        {
            MinimumLevelDefault = "Information",
            WriteTo = new List<SerilogSinkModel> { new() { Name = "Console" } }
        };

        // 7. Environment overrides
        var state = new WizardState
        {
            BaseModel = model,
            SetupAnswers = answers,
        };

        // Collect all unique environments across all products
        var allEnvironments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var productSetup in answers.Products)
        {
            foreach (var env in productSetup.Environments)
                allEnvironments.Add(env);
        }

        foreach (var env in allEnvironments)
        {
            var envModel = new ConfigurationModel
            {
                FilePath = $"appsettings.{env}.json",
                FileRole = ConfigFileRole.Environment,
            };

            // Create connection string overrides for each env
            envModel.Repository = new RepositoryModel
            {
                ConnectionString = $"{{ENV:REPO_CONNECTION_STRING_{SanitizeForEnv(env)}}}",
            };

            state.EnvironmentModels[env] = envModel;
        }

        // Product-environment models
        foreach (var productSetup in answers.Products)
        {
            foreach (var env in productSetup.Environments)
            {
                string key = $"{productSetup.Alias}.{env}";
                var peModel = new ConfigurationModel
                {
                    FilePath = $"appsettings.{productSetup.Alias}.{env}.json",
                    FileRole = ConfigFileRole.ProductEnvironment,
                };
                state.ProductEnvironmentModels[key] = peModel;
            }
        }

        return state;
    }

    /// <summary>
    /// Creates a full ConfigurationModel with sensible defaults for a single product+environment combination.
    /// Used when entering Detailed Configuration for the first time for a combination.
    /// </summary>
    public static ConfigurationModel ScaffoldCombination(
        string productAlias,
        string environmentName,
        ConfigurationModel? baseModel = null)
    {
        var model = new ConfigurationModel
        {
            FilePath = $"appsettings.{productAlias}.{environmentName}.json",
            FileRole = ConfigFileRole.ProductEnvironment,
        };

        // Repository — inherit DatabaseType and SchemaName from base if available
        var baseRepo = baseModel?.Repository;
        model.Repository = new RepositoryModel
        {
            DatabaseType = baseRepo?.DatabaseType ?? "SqlServer",
            ConnectionString = $"{{ENV:REPO_CONNECTION_STRING_{SanitizeForEnv(environmentName)}}}",
            SchemaName = baseRepo?.SchemaName ?? (baseRepo?.DatabaseType == "Sqlite" ? "" : "ray"),
        };

        // DatabaseLogging — inherit from base if available
        if (baseModel?.DatabaseLogging != null)
        {
            model.DatabaseLogging = new DatabaseLoggingModel
            {
                DatabaseType = baseModel.DatabaseLogging.DatabaseType,
                ConnectionString = $"{{ENV:DBLOG_CONNECTION_STRING_{SanitizeForEnv(environmentName)}}}",
                SchemaName = baseModel.DatabaseLogging.SchemaName,
            };
        }
        else
        {
            model.DatabaseLogging = new DatabaseLoggingModel
            {
                DatabaseType = baseRepo?.DatabaseType ?? "SqlServer",
                ConnectionString = $"{{ENV:DBLOG_CONNECTION_STRING_{SanitizeForEnv(environmentName)}}}",
                SchemaName = baseRepo?.DatabaseType == "Sqlite" ? "" : "ray",
            };
        }

        // ProductDefaults — copy from base if available
        var basePd = baseModel?.ProductDefaults;
        model.ProductDefaults = new ProductDefaultsModel
        {
            MigrationErrorAction = basePd?.MigrationErrorAction ?? "Terminate",
            RollbackErrorAction = basePd?.RollbackErrorAction ?? "Terminate",
            MigrationFilesExtension = basePd?.MigrationFilesExtension ?? "sql",
            MigrationRollbackFilesPreExtension = basePd?.MigrationRollbackFilesPreExtension ?? "rollback",
            MigrationFilesEncoding = basePd?.MigrationFilesEncoding ?? "UTF-8",
            RequireRollbackFile = basePd?.RequireRollbackFile ?? true,
            StopRollbackOnMissingRollbackFile = basePd?.StopRollbackOnMissingRollbackFile ?? true,
            TargetGroupDefaults = new TargetGroupDefaultsModel
            {
                TargetMigrationOrder = basePd?.TargetGroupDefaults.TargetMigrationOrder ?? "Successively",
                HashValidationScope = basePd?.TargetGroupDefaults.HashValidationScope ?? "File",
                StopRollbackOnMissingRollbackFile = basePd?.TargetGroupDefaults.StopRollbackOnMissingRollbackFile ?? true,
                TargetDefaults = new TargetDefaultsModel
                {
                    DbCommandTimeoutInSeconds = basePd?.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds ?? 20,
                    DbCommandMaxRetries = basePd?.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries ?? 0,
                    DbCommandWaitTimeInMsBeforeRetry = basePd?.TargetGroupDefaults.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry ?? 250,
                }
            }
        };

        // Products — copy structure from base, adapt ConnectionStrings for environment
        var baseProduct = baseModel?.Products.FirstOrDefault(p =>
            string.Equals(p.Alias, productAlias, StringComparison.OrdinalIgnoreCase));

        if (baseProduct != null)
        {
            var product = new ProductModel
            {
                Alias = baseProduct.Alias,
                MigrationFilesRootDirectory = baseProduct.MigrationFilesRootDirectory,
            };

            foreach (var baseTg in baseProduct.TargetGroups)
            {
                var targetGroup = new TargetGroupModel
                {
                    Alias = baseTg.Alias,
                    DatabaseType = baseTg.DatabaseType,
                };

                foreach (var baseTarget in baseTg.Targets)
                {
                    targetGroup.Targets.Add(new TargetModel
                    {
                        Alias = baseTarget.Alias,
                        ConnectionString = "{ENV:" +
                            $"{SanitizeForEnv(productAlias)}_{SanitizeForEnv(baseTg.Alias)}_{SanitizeForEnv(baseTarget.Alias)}_CONNECTION_STRING_{SanitizeForEnv(environmentName)}" + "}",
                    });
                }

                product.TargetGroups.Add(targetGroup);
            }

            model.Products.Add(product);
        }
        else
        {
            // No matching product in base — fall back to defaults
            var product = new ProductModel
            {
                Alias = productAlias,
                MigrationFilesRootDirectory = $"./Migrations/{productAlias}",
            };

            var targetGroup = new TargetGroupModel
            {
                Alias = "Backend",
                DatabaseType = baseRepo?.DatabaseType ?? "SqlServer",
            };

            targetGroup.Targets.Add(new TargetModel
            {
                Alias = "MainDB",
                ConnectionString = "{ENV:" +
                    $"{SanitizeForEnv(productAlias)}_BACKEND_MAINDB_CONNECTION_STRING_{SanitizeForEnv(environmentName)}" + "}",
            });

            product.TargetGroups.Add(targetGroup);
            model.Products.Add(product);
        }

        // Serilog — copy from base if available
        model.Serilog = new SerilogModel
        {
            MinimumLevelDefault = baseModel?.Serilog.MinimumLevelDefault ?? "Information",
            WriteTo = new List<SerilogSinkModel> { new() { Name = "Console" } }
        };

        return model;
    }

    private static string SanitizeForEnv(string alias)
    {
        return alias.ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('-', '_')
            .Replace('.', '_');
    }
}
