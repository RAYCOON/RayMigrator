using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Handles reading and writing RayMigrator configuration JSON.
/// IO-free: works with JSON strings, not file paths.
/// </summary>
public static class ConfigurationSerializer
{
    public static ConfigurationModel LoadFromJson(string json, string? filePath = null)
    {
        var model = new ConfigurationModel { FilePath = filePath };
        var doc = JsonNode.Parse(json);

        // Preserve the full original document for round-trip safety
        // (re-parse because JsonNode instances can only have one parent)
        model.PreservedDocument = JsonNode.Parse(json);

        var raySection = doc?["RayMigrator"];
        if (raySection == null)
            return model;

        // Repository
        var repoNode = raySection["Repository"];
        if (repoNode != null)
        {
            model.Repository = new RepositoryModel
            {
                DatabaseType = repoNode["DatabaseType"]?.GetValue<string>() ?? "SqlServer",
                ConnectionString = repoNode["ConnectionString"]?.GetValue<string>() ?? "",
                SchemaName = repoNode["SchemaName"]?.GetValue<string>() ?? "ray",
                TableBaseName = repoNode["TableBaseName"]?.GetValue<string>() ?? "",
                DbCommandTimeoutInSeconds = repoNode["DbCommandTimeoutInSeconds"]?.GetValue<int>() ?? 60,
                DbCommandMaxRetries = repoNode["DbCommandMaxRetries"]?.GetValue<int>() ?? 100,
                DbCommandWaitTimeInMsBeforeRetry = repoNode["DbCommandWaitTimeInMsBeforeRetry"]?.GetValue<int>() ?? 250,
            };
        }

        // DatabaseLogging
        var dbLogNode = raySection["DatabaseLogging"];
        if (dbLogNode != null)
        {
            model.DatabaseLogging = new DatabaseLoggingModel
            {
                DatabaseType = dbLogNode["DatabaseType"]?.GetValue<string>() ?? "SqlServer",
                ConnectionString = dbLogNode["ConnectionString"]?.GetValue<string>() ?? "",
                SchemaName = dbLogNode["SchemaName"]?.GetValue<string>() ?? "ray",
                TableBaseName = dbLogNode["TableBaseName"]?.GetValue<string>() ?? "",
                MinimumLevel = dbLogNode["MinimumLevel"]?.GetValue<string>() ?? "Information",
                DbCommandTimeoutInSeconds = dbLogNode["DbCommandTimeoutInSeconds"]?.GetValue<int>() ?? 20,
            };
        }

        // ProductDefaults
        var defaultsNode = raySection["ProductDefaults"];
        if (defaultsNode != null)
        {
            model.ProductDefaults = ParseProductDefaults(defaultsNode);
        }

        // Products
        var productsNode = raySection["Products"]?.AsArray();
        if (productsNode != null)
        {
            foreach (var prodNode in productsNode)
            {
                if (prodNode == null) continue;
                model.Products.Add(ParseProduct(prodNode));
            }
        }

        // Serilog
        var serilogNode = raySection["Serilog"];
        if (serilogNode != null)
        {
            model.Serilog = ParseSerilog(serilogNode);
        }

        // CliTools
        var cliToolsNode = raySection["CliTools"]?.AsArray();
        if (cliToolsNode != null)
        {
            foreach (var toolNode in cliToolsNode)
            {
                if (toolNode == null) continue;
                model.CliTools.Add(ParseCliTool(toolNode));
            }
        }

        model.IsModified = false;
        return model;
    }

    /// <summary>
    /// Serializes a configuration model to JSON.
    /// When <paramref name="baseModel"/> is provided, only sections that differ from the base
    /// are included (diff-based serialization for environment/product-environment files).
    /// </summary>
    public static string ToJson(ConfigurationModel model, ConfigurationModel? baseModel = null, bool indented = true)
    {
        var rayNode = baseModel != null
            ? BuildRayMigratorNodeDiff(model, baseModel)
            : BuildRayMigratorNode(model);

        JsonObject root;

        if (model.PreservedDocument is JsonObject preservedRoot)
        {
            // Merge: start from preserved document, update known keys
            root = JsonNode.Parse(preservedRoot.ToJsonString())!.AsObject();

            if (root["RayMigrator"] is JsonObject preservedRay)
            {
                // Known keys managed by the wizard
                var managedKeys = new[] { "Repository", "DatabaseLogging", "ProductDefaults", "Products", "Serilog", "CliTools" };

                // Remove managed keys from preserved, then re-add from model
                foreach (var key in managedKeys)
                    preservedRay.Remove(key);

                // Copy all keys from our built node into the preserved section
                foreach (var kvp in rayNode.AsObject().ToList())
                {
                    var cloned = kvp.Value != null ? JsonNode.Parse(kvp.Value.ToJsonString()) : null;
                    preservedRay[kvp.Key] = cloned;
                }
            }
            else
            {
                root["RayMigrator"] = rayNode;
            }
        }
        else
        {
            root = new JsonObject { ["RayMigrator"] = rayNode };
        }

        var options = new JsonSerializerOptions { WriteIndented = indented };
        return root.ToJsonString(options);
    }

    private static ProductDefaultsModel ParseProductDefaults(JsonNode node)
    {
        var model = new ProductDefaultsModel
        {
            MigrationErrorAction = node["MigrationErrorAction"]?.GetValue<string>() ?? "Terminate",
            RollbackErrorAction = node["RollbackErrorAction"]?.GetValue<string>() ?? "Terminate",
            MigrationFilesExtension = node["MigrationFilesExtension"]?.GetValue<string>() ?? "sql",
            MigrationRollbackFilesPreExtension = node["MigrationRollbackFilesPreExtension"]?.GetValue<string>() ?? "rollback",
            MigrationFilesEncoding = node["MigrationFilesEncoding"]?.GetValue<string>() ?? "UTF-8",
            RequireRollbackFile = node["RequireRollbackFile"]?.GetValue<bool>() ?? true,
            StopRollbackOnMissingRollbackFile = node["StopRollbackOnMissingRollbackFile"]?.GetValue<bool>() ?? true,
            UseCliToolAlias = node["UseCliToolAlias"]?.GetValue<string>(),
        };

        var tgDefNode = node["TargetGroupDefaults"];
        if (tgDefNode != null)
        {
            model.TargetGroupDefaults = new TargetGroupDefaultsModel
            {
                TargetMigrationOrder = tgDefNode["TargetMigrationOrder"]?.GetValue<string>() ?? "Successively",
                HashValidationScope = tgDefNode["HashValidationScope"]?.GetValue<string>() ?? "File",
                StopRollbackOnMissingRollbackFile = tgDefNode["StopRollbackOnMissingRollbackFile"]?.GetValue<bool>() ?? true,
            };

            var tdNode = tgDefNode["TargetDefaults"];
            if (tdNode != null)
            {
                model.TargetGroupDefaults.TargetDefaults = new TargetDefaultsModel
                {
                    DbCommandTimeoutInSeconds = tdNode["DbCommandTimeoutInSeconds"]?.GetValue<int>() ?? 20,
                    DbCommandMaxRetries = tdNode["DbCommandMaxRetries"]?.GetValue<int>() ?? 0,
                    DbCommandWaitTimeInMsBeforeRetry = tdNode["DbCommandWaitTimeInMsBeforeRetry"]?.GetValue<int>() ?? 250,
                };
            }
        }

        return model;
    }

    private static ProductModel ParseProduct(JsonNode node)
    {
        var product = new ProductModel
        {
            Alias = node["Alias"]?.GetValue<string>() ?? "",
            MigrationFilesRootDirectory = node["MigrationFilesRootDirectory"]?.GetValue<string>() ?? "",
        };

        ParseOverridableString(node, "MigrationErrorAction", product.MigrationErrorAction);
        ParseOverridableString(node, "RollbackErrorAction", product.RollbackErrorAction);
        ParseOverridableString(node, "MigrationFilesExtension", product.MigrationFilesExtension);
        ParseOverridableString(node, "MigrationRollbackFilesPreExtension", product.MigrationRollbackFilesPreExtension);
        ParseOverridableString(node, "MigrationFilesEncoding", product.MigrationFilesEncoding);
        ParseOverridableBool(node, "RequireRollbackFile", product.RequireRollbackFile);
        ParseOverridableBool(node, "StopRollbackOnMissingRollbackFile", product.StopRollbackOnMissingRollbackFile);
        ParseOverridableString(node, "UseCliToolAlias", product.UseCliToolAlias);
        product.TargetGroupMigrationOrder = node["TargetGroupMigrationOrder"]?.GetValue<string>();

        var tgArray = node["TargetGroups"]?.AsArray();
        if (tgArray != null)
        {
            foreach (var tgNode in tgArray)
            {
                if (tgNode == null) continue;
                product.TargetGroups.Add(ParseTargetGroup(tgNode));
            }
        }

        return product;
    }

    private static TargetGroupModel ParseTargetGroup(JsonNode node)
    {
        var tg = new TargetGroupModel
        {
            Alias = node["Alias"]?.GetValue<string>() ?? "",
            DatabaseType = node["DatabaseType"]?.GetValue<string>() ?? "SqlServer",
        };

        ParseOverridableString(node, "TargetMigrationOrder", tg.TargetMigrationOrder);
        ParseOverridableString(node, "HashValidationScope", tg.HashValidationScope);
        ParseOverridableString(node, "UseCliToolAlias", tg.UseCliToolAlias);
        ParseOverridableBool(node, "StopRollbackOnMissingRollbackFile", tg.StopRollbackOnMissingRollbackFile);

        var targetsArray = node["Targets"]?.AsArray();
        if (targetsArray != null)
        {
            foreach (var targetNode in targetsArray)
            {
                if (targetNode == null) continue;
                tg.Targets.Add(ParseTarget(targetNode));
            }
        }

        return tg;
    }

    private static TargetModel ParseTarget(JsonNode node)
    {
        var target = new TargetModel
        {
            Alias = node["Alias"]?.GetValue<string>() ?? "",
            ConnectionString = node["ConnectionString"]?.GetValue<string>() ?? "",
        };

        ParseOverridableInt(node, "DbCommandTimeoutInSeconds", target.DbCommandTimeoutInSeconds);
        ParseOverridableInt(node, "DbCommandMaxRetries", target.DbCommandMaxRetries);
        ParseOverridableInt(node, "DbCommandWaitTimeInMsBeforeRetry", target.DbCommandWaitTimeInMsBeforeRetry);
        ParseOverridableString(node, "UseCliToolAlias", target.UseCliToolAlias);

        // CliToolParameters
        var paramsNode = node["CliToolParameters"]?.AsObject();
        if (paramsNode != null)
        {
            target.CliToolParameters = new Dictionary<string, string>();
            foreach (var kvp in paramsNode)
            {
                target.CliToolParameters[kvp.Key] = kvp.Value?.GetValue<string>() ?? "";
            }
        }

        return target;
    }

    private static CliToolModel ParseCliTool(JsonNode node)
    {
        var tool = new CliToolModel
        {
            Alias = node["Alias"]?.GetValue<string>() ?? "",
            ExecutablePath = node["ExecutablePath"]?.GetValue<string>() ?? "",
            ArgumentTemplate = node["ArgumentTemplate"]?.GetValue<string>() ?? "",
            InputMode = node["InputMode"]?.GetValue<string>() ?? "File",
            CliToolTimeoutInSeconds = node["CliToolTimeoutInSeconds"]?.GetValue<int>() ?? 120,
        };

        var successCodes = node["SuccessExitCodes"]?.AsArray();
        if (successCodes != null)
        {
            tool.SuccessExitCodes = new List<string>();
            foreach (var code in successCodes)
            {
                if (code != null)
                    tool.SuccessExitCodes.Add(code.GetValue<string>());
            }
        }

        return tool;
    }

    private static SerilogModel ParseSerilog(JsonNode node)
    {
        var model = new SerilogModel();

        var minLevel = node["MinimumLevel"];
        if (minLevel != null)
        {
            if (minLevel is JsonValue val)
            {
                model.MinimumLevelDefault = val.GetValue<string>();
            }
            else
            {
                model.MinimumLevelDefault = minLevel["Default"]?.GetValue<string>() ?? "Information";
                var overrides = minLevel["Override"]?.AsObject();
                if (overrides != null)
                {
                    foreach (var kvp in overrides)
                    {
                        model.MinimumLevelOverrides[kvp.Key] = kvp.Value?.GetValue<string>() ?? "Information";
                    }
                }
            }
        }

        var writeTo = node["WriteTo"]?.AsArray();
        if (writeTo != null)
        {
            foreach (var sinkNode in writeTo)
            {
                if (sinkNode == null) continue;
                var sink = new SerilogSinkModel
                {
                    Name = sinkNode["Name"]?.GetValue<string>() ?? "Console",
                };

                var args = sinkNode["Args"]?.AsObject();
                if (args != null)
                {
                    foreach (var kvp in args)
                    {
                        if (kvp.Value is JsonValue jv && jv.TryGetValue<string>(out var str))
                            sink.Args[kvp.Key] = str;
                        else
                            sink.Args[kvp.Key] = kvp.Value?.ToString() ?? "";
                    }
                }

                model.WriteTo.Add(sink);
            }
        }

        return model;
    }

    private static JsonObject BuildRayMigratorNode(ConfigurationModel model)
    {
        var ray = new JsonObject();

        // Repository
        ray["Repository"] = new JsonObject
        {
            ["DatabaseType"] = model.Repository.DatabaseType,
            ["ConnectionString"] = model.Repository.ConnectionString,
            ["SchemaName"] = model.Repository.SchemaName,
            ["TableBaseName"] = model.Repository.TableBaseName,
            ["DbCommandTimeoutInSeconds"] = model.Repository.DbCommandTimeoutInSeconds,
            ["DbCommandMaxRetries"] = model.Repository.DbCommandMaxRetries,
            ["DbCommandWaitTimeInMsBeforeRetry"] = model.Repository.DbCommandWaitTimeInMsBeforeRetry,
        };

        // DatabaseLogging
        if (model.DatabaseLogging != null)
        {
            ray["DatabaseLogging"] = new JsonObject
            {
                ["DatabaseType"] = model.DatabaseLogging.DatabaseType,
                ["ConnectionString"] = model.DatabaseLogging.ConnectionString,
                ["SchemaName"] = model.DatabaseLogging.SchemaName,
                ["TableBaseName"] = model.DatabaseLogging.TableBaseName,
                ["MinimumLevel"] = model.DatabaseLogging.MinimumLevel,
                ["DbCommandTimeoutInSeconds"] = model.DatabaseLogging.DbCommandTimeoutInSeconds,
            };
        }

        // ProductDefaults
        var defaults = model.ProductDefaults;
        var defaultsObj = new JsonObject
        {
            ["MigrationErrorAction"] = defaults.MigrationErrorAction,
            ["RollbackErrorAction"] = defaults.RollbackErrorAction,
            ["MigrationFilesExtension"] = defaults.MigrationFilesExtension,
            ["MigrationRollbackFilesPreExtension"] = defaults.MigrationRollbackFilesPreExtension,
            ["MigrationFilesEncoding"] = defaults.MigrationFilesEncoding,
            ["RequireRollbackFile"] = defaults.RequireRollbackFile,
            ["StopRollbackOnMissingRollbackFile"] = defaults.StopRollbackOnMissingRollbackFile,
            ["TargetGroupDefaults"] = new JsonObject
            {
                ["TargetMigrationOrder"] = defaults.TargetGroupDefaults.TargetMigrationOrder,
                ["HashValidationScope"] = defaults.TargetGroupDefaults.HashValidationScope,
                ["StopRollbackOnMissingRollbackFile"] = defaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile,
                ["TargetDefaults"] = new JsonObject
                {
                    ["DbCommandTimeoutInSeconds"] = defaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds,
                    ["DbCommandMaxRetries"] = defaults.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries,
                    ["DbCommandWaitTimeInMsBeforeRetry"] = defaults.TargetGroupDefaults.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry,
                }
            }
        };

        if (defaults.UseCliToolAlias != null)
            defaultsObj["UseCliToolAlias"] = defaults.UseCliToolAlias;

        ray["ProductDefaults"] = defaultsObj;

        // Products
        var products = new JsonArray();
        foreach (var prod in model.Products)
        {
            var prodObj = new JsonObject
            {
                ["Alias"] = prod.Alias,
                ["MigrationFilesRootDirectory"] = prod.MigrationFilesRootDirectory,
            };

            WriteOverridableString(prodObj, "MigrationErrorAction", prod.MigrationErrorAction);
            WriteOverridableString(prodObj, "RollbackErrorAction", prod.RollbackErrorAction);
            WriteOverridableString(prodObj, "MigrationFilesExtension", prod.MigrationFilesExtension);
            WriteOverridableString(prodObj, "MigrationRollbackFilesPreExtension", prod.MigrationRollbackFilesPreExtension);
            WriteOverridableString(prodObj, "MigrationFilesEncoding", prod.MigrationFilesEncoding);
            WriteOverridableBool(prodObj, "RequireRollbackFile", prod.RequireRollbackFile);
            WriteOverridableBool(prodObj, "StopRollbackOnMissingRollbackFile", prod.StopRollbackOnMissingRollbackFile);
            WriteOverridableString(prodObj, "UseCliToolAlias", prod.UseCliToolAlias);
            if (prod.TargetGroupMigrationOrder != null)
                prodObj["TargetGroupMigrationOrder"] = prod.TargetGroupMigrationOrder;

            var tgArray = new JsonArray();
            foreach (var tg in prod.TargetGroups)
            {
                var tgObj = new JsonObject
                {
                    ["Alias"] = tg.Alias,
                    ["DatabaseType"] = tg.DatabaseType,
                };

                WriteOverridableString(tgObj, "TargetMigrationOrder", tg.TargetMigrationOrder);
                WriteOverridableString(tgObj, "HashValidationScope", tg.HashValidationScope);
                WriteOverridableString(tgObj, "UseCliToolAlias", tg.UseCliToolAlias);
                WriteOverridableBool(tgObj, "StopRollbackOnMissingRollbackFile", tg.StopRollbackOnMissingRollbackFile);

                var targetsArr = new JsonArray();
                foreach (var target in tg.Targets)
                {
                    var targetObj = new JsonObject
                    {
                        ["Alias"] = target.Alias,
                        ["ConnectionString"] = target.ConnectionString,
                    };

                    WriteOverridableInt(targetObj, "DbCommandTimeoutInSeconds", target.DbCommandTimeoutInSeconds);
                    WriteOverridableInt(targetObj, "DbCommandMaxRetries", target.DbCommandMaxRetries);
                    WriteOverridableInt(targetObj, "DbCommandWaitTimeInMsBeforeRetry", target.DbCommandWaitTimeInMsBeforeRetry);
                    WriteOverridableString(targetObj, "UseCliToolAlias", target.UseCliToolAlias);

                    // CliToolParameters: only write when the target actually resolves a CLI tool.
                    // Prevents orphan params from leaking into the saved JSON when the alias chain
                    // produces null (e.g. user removed the alias but params at a higher level remain).
                    var effectiveAlias = InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, prod, defaults);
                    if (!string.IsNullOrWhiteSpace(effectiveAlias))
                    {
                        // Inheritance walk: Target -> TargetGroup -> Product
                        var effectiveParams = target.CliToolParameters is { Count: > 0 }
                            ? target.CliToolParameters
                            : tg.CliToolParameters is { Count: > 0 }
                                ? tg.CliToolParameters
                                : prod.CliToolParameters;
                        if (effectiveParams is { Count: > 0 })
                        {
                            var paramsObj = new JsonObject();
                            foreach (var kvp in effectiveParams)
                            {
                                paramsObj[kvp.Key] = kvp.Value;
                            }
                            targetObj["CliToolParameters"] = paramsObj;
                        }
                    }

                    targetsArr.Add(targetObj);
                }
                tgObj["Targets"] = targetsArr;
                tgArray.Add(tgObj);
            }
            prodObj["TargetGroups"] = tgArray;
            products.Add(prodObj);
        }
        ray["Products"] = products;

        // Serilog
        var serilog = new JsonObject();
        var minLevel = new JsonObject { ["Default"] = model.Serilog.MinimumLevelDefault };
        if (model.Serilog.MinimumLevelOverrides.Count > 0)
        {
            var overrides = new JsonObject();
            foreach (var kvp in model.Serilog.MinimumLevelOverrides)
            {
                overrides[kvp.Key] = kvp.Value;
            }
            minLevel["Override"] = overrides;
        }
        serilog["MinimumLevel"] = minLevel;

        var writeToArr = new JsonArray();
        foreach (var sink in model.Serilog.WriteTo)
        {
            var sinkObj = new JsonObject { ["Name"] = sink.Name };
            if (sink.Args.Count > 0)
            {
                var argsObj = new JsonObject();
                foreach (var kvp in sink.Args)
                {
                    argsObj[kvp.Key] = kvp.Value;
                }
                sinkObj["Args"] = argsObj;
            }
            writeToArr.Add(sinkObj);
        }
        serilog["WriteTo"] = writeToArr;
        ray["Serilog"] = serilog;

        // CliTools (only if non-empty)
        if (model.CliTools.Count > 0)
        {
            var cliToolsArr = new JsonArray();
            foreach (var tool in model.CliTools)
            {
                var toolObj = new JsonObject
                {
                    ["Alias"] = tool.Alias,
                    ["ExecutablePath"] = tool.ExecutablePath,
                    ["ArgumentTemplate"] = tool.ArgumentTemplate,
                    ["InputMode"] = tool.InputMode,
                    ["CliToolTimeoutInSeconds"] = tool.CliToolTimeoutInSeconds,
                };

                var successArr = new JsonArray();
                foreach (var code in tool.SuccessExitCodes) successArr.Add(code);
                toolObj["SuccessExitCodes"] = successArr;

                cliToolsArr.Add(toolObj);
            }
            ray["CliTools"] = cliToolsArr;
        }

        return ray;
    }

    private static void ParseOverridableString(JsonNode parent, string key, OverridableValue<string> target)
    {
        var val = parent[key]?.GetValue<string>();
        if (val != null)
        {
            target.IsOverridden = true;
            target.Value = val;
        }
    }

    private static void ParseOverridableBool(JsonNode parent, string key, OverridableValue<bool> target)
    {
        var node = parent[key];
        if (node != null)
        {
            target.IsOverridden = true;
            target.Value = node.GetValue<bool>();
        }
    }

    private static void ParseOverridableInt(JsonNode parent, string key, OverridableValue<int> target)
    {
        var node = parent[key];
        if (node != null)
        {
            target.IsOverridden = true;
            target.Value = node.GetValue<int>();
        }
    }

    private static void WriteOverridableString(JsonObject obj, string key, OverridableValue<string> value)
    {
        if (value.IsOverridden && value.Value != null)
            obj[key] = value.Value;
    }

    private static void WriteOverridableBool(JsonObject obj, string key, OverridableValue<bool> value)
    {
        if (value.IsOverridden)
            obj[key] = value.Value;
    }

    private static void WriteOverridableInt(JsonObject obj, string key, OverridableValue<int> value)
    {
        if (value.IsOverridden)
            obj[key] = value.Value;
    }

    // ── Diff-based serialization ──────────────────────────────────

    /// <summary>
    /// Builds a RayMigrator JSON node containing only sections that differ from the base model.
    /// Compares each field against <paramref name="baseModel"/> so that environment/product-environment
    /// override files only contain meaningful overrides, not inherited values.
    /// </summary>
    private static JsonObject BuildRayMigratorNodeDiff(ConfigurationModel model, ConfigurationModel baseModel)
    {
        var ray = new JsonObject();

        // Repository — only include properties that differ from base
        var repoDiff = BuildRepositoryDiff(model.Repository, baseModel.Repository);
        if (repoDiff.Count > 0)
            ray["Repository"] = repoDiff;

        // DatabaseLogging — field-level diff when base also has it, full serialization when new
        if (model.DatabaseLogging != null)
        {
            if (baseModel.DatabaseLogging == null)
            {
                // Entirely new section — serialize all fields
                ray["DatabaseLogging"] = new JsonObject
                {
                    ["DatabaseType"] = model.DatabaseLogging.DatabaseType,
                    ["ConnectionString"] = model.DatabaseLogging.ConnectionString,
                    ["SchemaName"] = model.DatabaseLogging.SchemaName,
                    ["TableBaseName"] = model.DatabaseLogging.TableBaseName,
                    ["MinimumLevel"] = model.DatabaseLogging.MinimumLevel,
                    ["DbCommandTimeoutInSeconds"] = model.DatabaseLogging.DbCommandTimeoutInSeconds,
                };
            }
            else
            {
                var dblDiff = BuildDatabaseLoggingDiff(model.DatabaseLogging, baseModel.DatabaseLogging);
                if (dblDiff.Count > 0)
                    ray["DatabaseLogging"] = dblDiff;
            }
        }

        // ProductDefaults — field-level diff against base
        var pdDiff = BuildProductDefaultsDiff(model.ProductDefaults, baseModel.ProductDefaults);
        if (pdDiff.Count > 0)
            ray["ProductDefaults"] = pdDiff;

        // Products — alias-based field-level diff
        if (model.Products.Count > 0)
        {
            var productsDiff = BuildProductsDiff(model.Products, baseModel.Products, model.ProductDefaults);
            if (productsDiff.Count > 0)
                ray["Products"] = productsDiff;
        }

        // Serilog — field-level diff against base
        var serilogDiff = BuildSerilogDiff(model.Serilog, baseModel.Serilog);
        if (serilogDiff.Count > 0)
            ray["Serilog"] = serilogDiff;

        // CliTools — only if non-empty (same as full serialization)
        if (model.CliTools.Count > 0)
        {
            var fullNode = BuildRayMigratorNode(model);
            if (fullNode["CliTools"] is JsonNode cliNode)
                ray["CliTools"] = JsonNode.Parse(cliNode.ToJsonString());
        }

        return ray;
    }

    // ── Products alias-based diff ──────────────────────────────────

    private static JsonArray BuildProductsDiff(List<ProductModel> products, List<ProductModel> baseProducts, ProductDefaultsModel defaults)
    {
        var arr = new JsonArray();
        bool anyDiff = false;

        foreach (var product in products)
        {
            var baseProduct = baseProducts.FirstOrDefault(p =>
                string.Equals(p.Alias, product.Alias, StringComparison.OrdinalIgnoreCase));

            if (baseProduct != null)
            {
                var diff = BuildProductDiff(product, baseProduct, defaults);
                // Only include products with actual overrides (not just Alias)
                if (diff.Count > 1)
                {
                    arr.Add(diff);
                    anyDiff = true;
                }
            }
            else
            {
                // New product not in base — full serialization
                arr.Add(BuildFullProduct(product, defaults));
                anyDiff = true;
            }
        }

        return anyDiff ? arr : new JsonArray();
    }

    private static JsonObject BuildProductDiff(ProductModel product, ProductModel baseProduct, ProductDefaultsModel defaults)
    {
        var obj = new JsonObject { ["Alias"] = product.Alias };

        if (product.MigrationFilesRootDirectory != baseProduct.MigrationFilesRootDirectory)
            obj["MigrationFilesRootDirectory"] = product.MigrationFilesRootDirectory;

        // Overridable product-level fields — only if overridden AND different from base
        WriteOverridableDiffString(obj, "MigrationErrorAction", product.MigrationErrorAction, baseProduct.MigrationErrorAction);
        WriteOverridableDiffString(obj, "RollbackErrorAction", product.RollbackErrorAction, baseProduct.RollbackErrorAction);
        WriteOverridableDiffString(obj, "MigrationFilesExtension", product.MigrationFilesExtension, baseProduct.MigrationFilesExtension);
        WriteOverridableDiffString(obj, "MigrationRollbackFilesPreExtension", product.MigrationRollbackFilesPreExtension, baseProduct.MigrationRollbackFilesPreExtension);
        WriteOverridableDiffString(obj, "MigrationFilesEncoding", product.MigrationFilesEncoding, baseProduct.MigrationFilesEncoding);
        WriteOverridableDiffBool(obj, "RequireRollbackFile", product.RequireRollbackFile, baseProduct.RequireRollbackFile);
        WriteOverridableDiffBool(obj, "StopRollbackOnMissingRollbackFile", product.StopRollbackOnMissingRollbackFile, baseProduct.StopRollbackOnMissingRollbackFile);
        WriteOverridableDiffString(obj, "UseCliToolAlias", product.UseCliToolAlias, baseProduct.UseCliToolAlias);

        if (product.TargetGroupMigrationOrder != baseProduct.TargetGroupMigrationOrder && product.TargetGroupMigrationOrder != null)
            obj["TargetGroupMigrationOrder"] = product.TargetGroupMigrationOrder;

        // TargetGroups diff
        var tgDiff = BuildTargetGroupsDiff(product, product.TargetGroups, baseProduct.TargetGroups, defaults);
        if (tgDiff.Count > 0)
            obj["TargetGroups"] = tgDiff;

        return obj;
    }

    private static JsonArray BuildTargetGroupsDiff(ProductModel product, List<TargetGroupModel> targetGroups, List<TargetGroupModel> baseTargetGroups, ProductDefaultsModel defaults)
    {
        var arr = new JsonArray();
        bool anyDiff = false;

        foreach (var tg in targetGroups)
        {
            var baseTg = baseTargetGroups.FirstOrDefault(b =>
                string.Equals(b.Alias, tg.Alias, StringComparison.OrdinalIgnoreCase));

            if (baseTg != null)
            {
                var diff = BuildTargetGroupDiff(product, tg, baseTg, defaults);
                // Only include target groups with actual overrides (not just Alias + DatabaseType)
                if (diff.Count > 2)
                {
                    arr.Add(diff);
                    anyDiff = true;
                }
            }
            else
            {
                arr.Add(BuildFullTargetGroup(product, tg, defaults));
                anyDiff = true;
            }
        }

        return anyDiff ? arr : new JsonArray();
    }

    private static JsonObject BuildTargetGroupDiff(ProductModel product, TargetGroupModel tg, TargetGroupModel baseTg, ProductDefaultsModel defaults)
    {
        var obj = new JsonObject
        {
            ["Alias"] = tg.Alias,
            ["DatabaseType"] = tg.DatabaseType,
        };

        WriteOverridableDiffString(obj, "TargetMigrationOrder", tg.TargetMigrationOrder, baseTg.TargetMigrationOrder);
        WriteOverridableDiffString(obj, "HashValidationScope", tg.HashValidationScope, baseTg.HashValidationScope);
        WriteOverridableDiffString(obj, "UseCliToolAlias", tg.UseCliToolAlias, baseTg.UseCliToolAlias);
        WriteOverridableDiffBool(obj, "StopRollbackOnMissingRollbackFile", tg.StopRollbackOnMissingRollbackFile, baseTg.StopRollbackOnMissingRollbackFile);

        // Targets diff
        var targetsDiff = BuildTargetsDiff(product, tg, tg.Targets, baseTg.Targets, defaults);
        if (targetsDiff.Count > 0)
            obj["Targets"] = targetsDiff;

        return obj;
    }

    private static JsonArray BuildTargetsDiff(ProductModel product, TargetGroupModel tg, List<TargetModel> targets, List<TargetModel> baseTargets, ProductDefaultsModel defaults)
    {
        var arr = new JsonArray();
        bool anyDiff = false;

        foreach (var target in targets)
        {
            var baseTarget = baseTargets.FirstOrDefault(b =>
                string.Equals(b.Alias, target.Alias, StringComparison.OrdinalIgnoreCase));

            if (baseTarget != null)
            {
                var diff = BuildTargetDiff(product, tg, target, baseTarget, defaults);
                // Only include targets with actual overrides (not just Alias)
                if (diff.Count > 1)
                {
                    arr.Add(diff);
                    anyDiff = true;
                }
            }
            else
            {
                arr.Add(BuildFullTarget(product, tg, target, defaults));
                anyDiff = true;
            }
        }

        return anyDiff ? arr : new JsonArray();
    }

    private static JsonObject BuildTargetDiff(ProductModel product, TargetGroupModel tg, TargetModel target, TargetModel baseTarget, ProductDefaultsModel defaults)
    {
        var obj = new JsonObject { ["Alias"] = target.Alias };

        if (target.ConnectionString != baseTarget.ConnectionString)
            obj["ConnectionString"] = target.ConnectionString;

        WriteOverridableDiffInt(obj, "DbCommandTimeoutInSeconds", target.DbCommandTimeoutInSeconds, baseTarget.DbCommandTimeoutInSeconds);
        WriteOverridableDiffInt(obj, "DbCommandMaxRetries", target.DbCommandMaxRetries, baseTarget.DbCommandMaxRetries);
        WriteOverridableDiffInt(obj, "DbCommandWaitTimeInMsBeforeRetry", target.DbCommandWaitTimeInMsBeforeRetry, baseTarget.DbCommandWaitTimeInMsBeforeRetry);
        WriteOverridableDiffString(obj, "UseCliToolAlias", target.UseCliToolAlias, baseTarget.UseCliToolAlias);

        // CliToolParameters: only write when the target resolves a CLI tool.
        var effectiveAlias = InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, product, defaults);
        if (!string.IsNullOrWhiteSpace(effectiveAlias))
        {
            var effectiveParams = target.CliToolParameters is { Count: > 0 }
                ? target.CliToolParameters
                : tg.CliToolParameters is { Count: > 0 }
                    ? tg.CliToolParameters
                    : product.CliToolParameters;
            if (effectiveParams is { Count: > 0 })
            {
                var baseEffective = baseTarget.CliToolParameters;
                bool cliParamsDiffer = baseEffective == null
                    || effectiveParams.Count != baseEffective.Count
                    || effectiveParams.Any(kvp =>
                        !baseEffective.TryGetValue(kvp.Key, out var baseVal) || kvp.Value != baseVal);

                if (cliParamsDiffer)
                {
                    var paramsObj = new JsonObject();
                    foreach (var kvp in effectiveParams)
                        paramsObj[kvp.Key] = kvp.Value;
                    obj["CliToolParameters"] = paramsObj;
                }
            }
        }

        return obj;
    }

    // Full serialization helpers for unmatched (new) elements

    private static JsonObject BuildFullProduct(ProductModel prod, ProductDefaultsModel defaults)
    {
        var prodObj = new JsonObject
        {
            ["Alias"] = prod.Alias,
            ["MigrationFilesRootDirectory"] = prod.MigrationFilesRootDirectory,
        };

        WriteOverridableString(prodObj, "MigrationErrorAction", prod.MigrationErrorAction);
        WriteOverridableString(prodObj, "RollbackErrorAction", prod.RollbackErrorAction);
        WriteOverridableString(prodObj, "MigrationFilesExtension", prod.MigrationFilesExtension);
        WriteOverridableString(prodObj, "MigrationRollbackFilesPreExtension", prod.MigrationRollbackFilesPreExtension);
        WriteOverridableString(prodObj, "MigrationFilesEncoding", prod.MigrationFilesEncoding);
        WriteOverridableBool(prodObj, "RequireRollbackFile", prod.RequireRollbackFile);
        WriteOverridableBool(prodObj, "StopRollbackOnMissingRollbackFile", prod.StopRollbackOnMissingRollbackFile);
        WriteOverridableString(prodObj, "UseCliToolAlias", prod.UseCliToolAlias);
        if (prod.TargetGroupMigrationOrder != null)
            prodObj["TargetGroupMigrationOrder"] = prod.TargetGroupMigrationOrder;

        var tgArray = new JsonArray();
        foreach (var tg in prod.TargetGroups)
            tgArray.Add(BuildFullTargetGroup(prod, tg, defaults));
        prodObj["TargetGroups"] = tgArray;

        return prodObj;
    }

    private static JsonObject BuildFullTargetGroup(ProductModel product, TargetGroupModel tg, ProductDefaultsModel defaults)
    {
        var tgObj = new JsonObject
        {
            ["Alias"] = tg.Alias,
            ["DatabaseType"] = tg.DatabaseType,
        };

        WriteOverridableString(tgObj, "TargetMigrationOrder", tg.TargetMigrationOrder);
        WriteOverridableString(tgObj, "HashValidationScope", tg.HashValidationScope);
        WriteOverridableString(tgObj, "UseCliToolAlias", tg.UseCliToolAlias);
        WriteOverridableBool(tgObj, "StopRollbackOnMissingRollbackFile", tg.StopRollbackOnMissingRollbackFile);

        var targetsArr = new JsonArray();
        foreach (var target in tg.Targets)
            targetsArr.Add(BuildFullTarget(product, tg, target, defaults));
        tgObj["Targets"] = targetsArr;

        return tgObj;
    }

    private static JsonObject BuildFullTarget(ProductModel product, TargetGroupModel tg, TargetModel target, ProductDefaultsModel defaults)
    {
        var targetObj = new JsonObject
        {
            ["Alias"] = target.Alias,
            ["ConnectionString"] = target.ConnectionString,
        };

        WriteOverridableInt(targetObj, "DbCommandTimeoutInSeconds", target.DbCommandTimeoutInSeconds);
        WriteOverridableInt(targetObj, "DbCommandMaxRetries", target.DbCommandMaxRetries);
        WriteOverridableInt(targetObj, "DbCommandWaitTimeInMsBeforeRetry", target.DbCommandWaitTimeInMsBeforeRetry);
        WriteOverridableString(targetObj, "UseCliToolAlias", target.UseCliToolAlias);

        // CliToolParameters: only write when the target resolves a CLI tool.
        var effectiveAlias = InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, product, defaults);
        if (!string.IsNullOrWhiteSpace(effectiveAlias))
        {
            var effectiveParams = target.CliToolParameters is { Count: > 0 }
                ? target.CliToolParameters
                : tg.CliToolParameters is { Count: > 0 }
                    ? tg.CliToolParameters
                    : product.CliToolParameters;
            if (effectiveParams is { Count: > 0 })
            {
                var paramsObj = new JsonObject();
                foreach (var kvp in effectiveParams)
                    paramsObj[kvp.Key] = kvp.Value;
                targetObj["CliToolParameters"] = paramsObj;
            }
        }

        return targetObj;
    }

    // Diff helpers for OverridableValue — include only if overridden AND different from base

    private static void WriteOverridableDiffString(JsonObject obj, string key, OverridableValue<string> value, OverridableValue<string> baseValue)
    {
        if (!value.IsOverridden)
            return;
        if (baseValue.IsOverridden && value.Value == baseValue.Value)
            return;
        if (value.Value != null)
            obj[key] = value.Value;
    }

    private static void WriteOverridableDiffBool(JsonObject obj, string key, OverridableValue<bool> value, OverridableValue<bool> baseValue)
    {
        if (!value.IsOverridden)
            return;
        if (baseValue.IsOverridden && value.Value == baseValue.Value)
            return;
        obj[key] = value.Value;
    }

    private static void WriteOverridableDiffInt(JsonObject obj, string key, OverridableValue<int> value, OverridableValue<int> baseValue)
    {
        if (!value.IsOverridden)
            return;
        if (baseValue.IsOverridden && value.Value == baseValue.Value)
            return;
        obj[key] = value.Value;
    }

    private static JsonObject BuildRepositoryDiff(RepositoryModel model, RepositoryModel baseRepo)
    {
        var obj = new JsonObject();
        if (!string.IsNullOrEmpty(model.DatabaseType) && model.DatabaseType != baseRepo.DatabaseType)
            obj["DatabaseType"] = model.DatabaseType;
        if (!string.IsNullOrEmpty(model.ConnectionString) && model.ConnectionString != baseRepo.ConnectionString)
            obj["ConnectionString"] = model.ConnectionString;
        if (!string.IsNullOrEmpty(model.SchemaName) && model.SchemaName != baseRepo.SchemaName)
            obj["SchemaName"] = model.SchemaName;
        if (model.TableBaseName != baseRepo.TableBaseName)
            obj["TableBaseName"] = model.TableBaseName;
        if (model.DbCommandTimeoutInSeconds != baseRepo.DbCommandTimeoutInSeconds)
            obj["DbCommandTimeoutInSeconds"] = model.DbCommandTimeoutInSeconds;
        if (model.DbCommandMaxRetries != baseRepo.DbCommandMaxRetries)
            obj["DbCommandMaxRetries"] = model.DbCommandMaxRetries;
        if (model.DbCommandWaitTimeInMsBeforeRetry != baseRepo.DbCommandWaitTimeInMsBeforeRetry)
            obj["DbCommandWaitTimeInMsBeforeRetry"] = model.DbCommandWaitTimeInMsBeforeRetry;
        return obj;
    }

    private static JsonObject BuildProductDefaultsDiff(ProductDefaultsModel model, ProductDefaultsModel basePd)
    {
        var obj = new JsonObject();
        // String fields cleared to "" by PromoteAcrossModels — skip empty (means "inherit from parent")
        if (!string.IsNullOrEmpty(model.MigrationErrorAction) && model.MigrationErrorAction != basePd.MigrationErrorAction)
            obj["MigrationErrorAction"] = model.MigrationErrorAction;
        if (!string.IsNullOrEmpty(model.RollbackErrorAction) && model.RollbackErrorAction != basePd.RollbackErrorAction)
            obj["RollbackErrorAction"] = model.RollbackErrorAction;
        if (!string.IsNullOrEmpty(model.MigrationFilesExtension) && model.MigrationFilesExtension != basePd.MigrationFilesExtension)
            obj["MigrationFilesExtension"] = model.MigrationFilesExtension;
        // MigrationRollbackFilesPreExtension: NOT cleared by PromoteAcrossModels — no empty guard
        if (model.MigrationRollbackFilesPreExtension != basePd.MigrationRollbackFilesPreExtension)
            obj["MigrationRollbackFilesPreExtension"] = model.MigrationRollbackFilesPreExtension;
        if (!string.IsNullOrEmpty(model.MigrationFilesEncoding) && model.MigrationFilesEncoding != basePd.MigrationFilesEncoding)
            obj["MigrationFilesEncoding"] = model.MigrationFilesEncoding;
        if (model.RequireRollbackFile != basePd.RequireRollbackFile)
            obj["RequireRollbackFile"] = model.RequireRollbackFile;
        if (model.StopRollbackOnMissingRollbackFile != basePd.StopRollbackOnMissingRollbackFile)
            obj["StopRollbackOnMissingRollbackFile"] = model.StopRollbackOnMissingRollbackFile;
        if (model.UseCliToolAlias != basePd.UseCliToolAlias)
        {
            if (model.UseCliToolAlias != null)
                obj["UseCliToolAlias"] = model.UseCliToolAlias;
        }

        var tgdDiff = BuildTargetGroupDefaultsDiff(model.TargetGroupDefaults, basePd.TargetGroupDefaults);
        if (tgdDiff.Count > 0)
            obj["TargetGroupDefaults"] = tgdDiff;

        return obj;
    }

    private static JsonObject BuildTargetGroupDefaultsDiff(TargetGroupDefaultsModel model, TargetGroupDefaultsModel baseTgd)
    {
        var obj = new JsonObject();
        if (model.TargetMigrationOrder != baseTgd.TargetMigrationOrder)
            obj["TargetMigrationOrder"] = model.TargetMigrationOrder;
        if (model.HashValidationScope != baseTgd.HashValidationScope)
            obj["HashValidationScope"] = model.HashValidationScope;
        if (model.StopRollbackOnMissingRollbackFile != baseTgd.StopRollbackOnMissingRollbackFile)
            obj["StopRollbackOnMissingRollbackFile"] = model.StopRollbackOnMissingRollbackFile;

        var tdDiff = BuildTargetDefaultsDiff(model.TargetDefaults, baseTgd.TargetDefaults);
        if (tdDiff.Count > 0)
            obj["TargetDefaults"] = tdDiff;

        return obj;
    }

    private static JsonObject BuildTargetDefaultsDiff(TargetDefaultsModel model, TargetDefaultsModel baseTd)
    {
        var obj = new JsonObject();
        if (model.DbCommandTimeoutInSeconds != baseTd.DbCommandTimeoutInSeconds)
            obj["DbCommandTimeoutInSeconds"] = model.DbCommandTimeoutInSeconds;
        if (model.DbCommandMaxRetries != baseTd.DbCommandMaxRetries)
            obj["DbCommandMaxRetries"] = model.DbCommandMaxRetries;
        if (model.DbCommandWaitTimeInMsBeforeRetry != baseTd.DbCommandWaitTimeInMsBeforeRetry)
            obj["DbCommandWaitTimeInMsBeforeRetry"] = model.DbCommandWaitTimeInMsBeforeRetry;
        return obj;
    }

    private static JsonObject BuildDatabaseLoggingDiff(DatabaseLoggingModel model, DatabaseLoggingModel baseDbl)
    {
        var obj = new JsonObject();
        if (!string.IsNullOrEmpty(model.DatabaseType) && model.DatabaseType != baseDbl.DatabaseType)
            obj["DatabaseType"] = model.DatabaseType;
        if (!string.IsNullOrEmpty(model.ConnectionString) && model.ConnectionString != baseDbl.ConnectionString)
            obj["ConnectionString"] = model.ConnectionString;
        if (!string.IsNullOrEmpty(model.SchemaName) && model.SchemaName != baseDbl.SchemaName)
            obj["SchemaName"] = model.SchemaName;
        if (model.TableBaseName != baseDbl.TableBaseName)
            obj["TableBaseName"] = model.TableBaseName;
        if (model.MinimumLevel != baseDbl.MinimumLevel)
            obj["MinimumLevel"] = model.MinimumLevel;
        if (model.DbCommandTimeoutInSeconds != baseDbl.DbCommandTimeoutInSeconds)
            obj["DbCommandTimeoutInSeconds"] = model.DbCommandTimeoutInSeconds;
        return obj;
    }

    private static JsonObject BuildSerilogDiff(SerilogModel model, SerilogModel baseSerilog)
    {
        var obj = new JsonObject();

        // MinimumLevel — skip empty (cleared to "" by PromoteAcrossModels)
        var minLevelChanged = !string.IsNullOrEmpty(model.MinimumLevelDefault)
                              && model.MinimumLevelDefault != baseSerilog.MinimumLevelDefault;
        var overridesChanged = !DictionaryEquals(model.MinimumLevelOverrides, baseSerilog.MinimumLevelOverrides);

        if (minLevelChanged || overridesChanged)
        {
            var minLevel = new JsonObject();
            if (minLevelChanged)
                minLevel["Default"] = model.MinimumLevelDefault;
            if (overridesChanged && model.MinimumLevelOverrides.Count > 0)
            {
                // Note: when model has empty overrides and base has entries, the Override key
                // is intentionally omitted — .NET configuration cannot remove keys via override files.
                var overrides = new JsonObject();
                foreach (var kvp in model.MinimumLevelOverrides)
                    overrides[kvp.Key] = kvp.Value;
                minLevel["Override"] = overrides;
            }
            if (minLevel.Count > 0)
                obj["MinimumLevel"] = minLevel;
        }

        // WriteTo — an empty model list means "not configured / inherit from base", not "zero sinks"
        if (model.WriteTo.Count > 0 && !WriteToEquals(model.WriteTo, baseSerilog.WriteTo))
        {
            var writeToArr = new JsonArray();
            foreach (var sink in model.WriteTo)
            {
                var sinkObj = new JsonObject { ["Name"] = sink.Name };
                if (sink.Args.Count > 0)
                {
                    var argsObj = new JsonObject();
                    foreach (var kvp in sink.Args)
                        argsObj[kvp.Key] = kvp.Value;
                    sinkObj["Args"] = argsObj;
                }
                writeToArr.Add(sinkObj);
            }
            obj["WriteTo"] = writeToArr;
        }

        return obj;
    }

    /// <summary>
    /// Returns true when the diff JSON contains only the wrapper <c>{"RayMigrator":{}}</c>
    /// with no actual override properties. Used by callers to skip writing empty override files.
    /// </summary>
    public static bool IsEmptyDiff(string json)
    {
        var doc = JsonNode.Parse(json);
        if (doc?["RayMigrator"] is not JsonObject ray)
            return true;
        return ray.Count == 0;
    }

    private static bool DictionaryEquals(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var bVal) || kvp.Value != bVal)
                return false;
        }
        return true;
    }

    private static bool WriteToEquals(List<SerilogSinkModel> a, List<SerilogSinkModel> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Name != b[i].Name) return false;
            if (!DictionaryEquals(a[i].Args, b[i].Args)) return false;
        }
        return true;
    }
}
