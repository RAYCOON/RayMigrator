// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Text.Json;
using System.Text.RegularExpressions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Fluent builder for engine test scenarios.
/// Configures migration file mutations, error injection, and runtime options
/// before building a ScenarioContext that executes the migration pipeline.
/// </summary>
public class ScenarioBuilder
{
    private readonly EngineConfig _engineConfig;
    private readonly List<Action<string>> _fileMutations = new();
    private MigrationErrorAction? _migrationErrorAction;
    private RollbackErrorAction? _rollbackErrorAction;
    private bool? _requireRollbackFile;
    private bool? _stopRollbackOnMissingRollbackFile;
    private TargetMigrationOrder? _targetMigrationOrder;
    private string? _secondConnectionString;
    private readonly List<(string alias, string databaseType, string connectionString,
        TargetMigrationOrder? order, HashValidationScope? hashScope)> _additionalTargetGroups = new();
    private bool _databaseLogging;
    private string _databaseLoggingMinLevel = "Debug";
    private readonly List<Dictionary<string, object>> _cliTools = new();
    private string? _useCliToolAlias;
    private Dictionary<string, string>? _targetCliToolParameters;
    private string? _targetGroupMigrationOrder;
    private int? _targetMaxRetries;
    private int? _targetRetryDelayMs;
    private int? _targetCommandTimeoutSeconds;

    public ScenarioBuilder(EngineConfig engineConfig)
    {
        _engineConfig = engineConfig;
    }

    /// <summary>
    /// Replaces the SQL body of a migration file with error-producing SQL.
    /// The file is identified by release folder name and filename.
    /// </summary>
    public ScenarioBuilder InjectError(string release, string filename)
    {
        _fileMutations.Add(workDir =>
        {
            string filePath = Path.Combine(workDir, release, "Backend", filename);
            string content = File.ReadAllText(filePath);
            var (toml, _) = ExtractTomlAndSql(content);
            string errorSql = SqlDialect.GetErrorSql(_engineConfig.DatabaseType);
            File.WriteAllText(filePath, toml + Environment.NewLine + Environment.NewLine + errorSql + Environment.NewLine);
        });
        return this;
    }

    /// <summary>
    /// Removes the rollback (.rollback.sql) file for the specified migration.
    /// </summary>
    public ScenarioBuilder RemoveRollback(string release, string filename)
    {
        _fileMutations.Add(workDir =>
        {
            string rollbackFilename = GetRollbackFilename(filename);
            string rollbackPath = Path.Combine(workDir, release, "Backend", rollbackFilename);
            if (File.Exists(rollbackPath))
                File.Delete(rollbackPath);
        });
        return this;
    }

    /// <summary>
    /// Replaces the rollback file content with SQL that will fail at runtime.
    /// </summary>
    public ScenarioBuilder BreakRollback(string release, string filename)
    {
        _fileMutations.Add(workDir =>
        {
            string rollbackFilename = GetRollbackFilename(filename);
            string rollbackPath = Path.Combine(workDir, release, "Backend", rollbackFilename);
            string content = File.ReadAllText(rollbackPath);
            var (toml, _) = ExtractTomlAndSql(content);
            string brokenSql = SqlDialect.GetBrokenRollbackSql(_engineConfig.DatabaseType);
            File.WriteAllText(rollbackPath, toml + Environment.NewLine + Environment.NewLine + brokenSql + Environment.NewLine);
        });
        return this;
    }

    /// <summary>
    /// Overrides a specific TOML metadata key in a migration file's header.
    /// </summary>
    public ScenarioBuilder SetFileToml(string release, string filename, string key, string value)
    {
        _fileMutations.Add(workDir =>
        {
            string filePath = Path.Combine(workDir, release, "Backend", filename);
            string content = File.ReadAllText(filePath);

            // Find the TOML block between /* and */
            int tomlStart = content.IndexOf("/*", StringComparison.Ordinal);
            int tomlEnd = tomlStart >= 0 ? content.IndexOf("*/", tomlStart, StringComparison.Ordinal) : -1;

            string tomlBlock;
            string sqlBody;

            if (tomlStart < 0 || tomlEnd < 0)
            {
                // No existing header — create a new one and treat the entire content as SQL
                tomlBlock = $"/*{Environment.NewLine}[RayMigrator]{Environment.NewLine}*/";
                sqlBody = Environment.NewLine + content;
            }
            else
            {
                tomlBlock = content.Substring(tomlStart, tomlEnd + 2 - tomlStart);
                sqlBody = content.Substring(tomlEnd + 2);
            }

            // Check if key already exists in the TOML block
            string keyPattern = $@"^{Regex.Escape(key)}\s*=\s*.*$";
            var regex = new Regex(keyPattern, RegexOptions.Multiline);

            string newLine = $"{key} = {value}";

            if (regex.IsMatch(tomlBlock))
            {
                // Replace existing key
                tomlBlock = regex.Replace(tomlBlock, newLine);
            }
            else
            {
                // Insert before the closing */
                int insertPos = tomlBlock.LastIndexOf("*/", StringComparison.Ordinal);
                tomlBlock = tomlBlock.Substring(0, insertPos) + newLine + Environment.NewLine + "*/";
            }

            File.WriteAllText(filePath, tomlBlock + sqlBody);
        });
        return this;
    }

    /// <summary>
    /// Injects error SQL at a specific block index within a multi-block migration file.
    /// Blocks are separated by GO on its own line (SQL Server style).
    /// </summary>
    public ScenarioBuilder InjectErrorAtBlock(string release, string filename, int blockIndex)
    {
        _fileMutations.Add(workDir =>
        {
            string filePath = Path.Combine(workDir, release, "Backend", filename);
            string content = File.ReadAllText(filePath);
            var (toml, sql) = ExtractTomlAndSql(content);

            // Split SQL by GO on its own line (case-insensitive)
            string[] blocks = Regex.Split(sql, @"(?m)^\s*GO\s*$", RegexOptions.IgnoreCase);

            if (blockIndex < 0 || blockIndex >= blocks.Length)
                throw new ArgumentOutOfRangeException(nameof(blockIndex),
                    $"Block index {blockIndex} is out of range. File has {blocks.Length} blocks.");

            string errorSql = SqlDialect.GetErrorSql(_engineConfig.DatabaseType);
            blocks[blockIndex] = Environment.NewLine + errorSql + Environment.NewLine;

            string reassembled = string.Join(Environment.NewLine + "GO" + Environment.NewLine, blocks);
            File.WriteAllText(filePath, toml + Environment.NewLine + Environment.NewLine + reassembled + Environment.NewLine);
        });
        return this;
    }

    /// <summary>
    /// Sets the MigrationErrorAction for the scenario.
    /// </summary>
    public ScenarioBuilder WithMigrationErrorAction(MigrationErrorAction action)
    {
        _migrationErrorAction = action;
        return this;
    }

    /// <summary>
    /// Sets the RollbackErrorAction for the scenario.
    /// </summary>
    public ScenarioBuilder WithRollbackErrorAction(RollbackErrorAction action)
    {
        _rollbackErrorAction = action;
        return this;
    }

    /// <summary>
    /// Sets whether rollback files are required for migration files.
    /// </summary>
    public ScenarioBuilder WithRequireRollbackFile(bool require)
    {
        _requireRollbackFile = require;
        return this;
    }

    /// <summary>
    /// Sets whether error-recovery rollback chains stop when a rollback file is missing.
    /// </summary>
    public ScenarioBuilder WithStopRollbackOnMissingRollbackFile(bool stop)
    {
        _stopRollbackOnMissingRollbackFile = stop;
        return this;
    }

    /// <summary>
    /// Sets the TargetMigrationOrder (Simultaneously or Successively) for target group execution.
    /// </summary>
    public ScenarioBuilder WithTargetMigrationOrder(TargetMigrationOrder order)
    {
        _targetMigrationOrder = order;
        return this;
    }

    /// <summary>
    /// Enables multi-target mode using the specified secondary connection string.
    /// </summary>
    public ScenarioBuilder WithMultiTarget(string secondConnectionString)
    {
        _secondConnectionString = secondConnectionString;
        return this;
    }

    /// <summary>
    /// Creates a .migsettings file at the specified relative path with key-value entries.
    /// </summary>
    public ScenarioBuilder SetMigSettings(string relativeFilePath, Dictionary<string, string> entries)
    {
        _fileMutations.Add(workDir =>
        {
            string fullPath = Path.Combine(workDir, relativeFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var lines = entries.Select(kv => $"{kv.Key} = {kv.Value}");
            string content = "[RayMigrator]" + Environment.NewLine + string.Join(Environment.NewLine, lines);
            File.WriteAllText(fullPath, content);
        });
        return this;
    }

    /// <summary>
    /// Adds an additional target group with its own database type and connection string.
    /// </summary>
    public ScenarioBuilder WithTargetGroup(string alias, string databaseType, string connectionString,
        TargetMigrationOrder? targetMigrationOrder = null, HashValidationScope? hashValidationScope = null)
    {
        _additionalTargetGroups.Add((alias, databaseType, connectionString, targetMigrationOrder, hashValidationScope));
        return this;
    }

    /// <summary>
    /// Adds a CLI tool definition for external SQL execution.
    /// </summary>
    public ScenarioBuilder WithCliTool(string alias, string executablePath,
        string argumentTemplate, string inputMode = "File",
        int? timeoutInSeconds = null, string[]? successExitCodes = null)
    {
        var tool = new Dictionary<string, object>
        {
            ["Alias"] = alias,
            ["ExecutablePath"] = executablePath,
            ["ArgumentTemplate"] = argumentTemplate,
            ["InputMode"] = inputMode
        };
        if (timeoutInSeconds.HasValue) tool["CliToolTimeoutInSeconds"] = timeoutInSeconds.Value;
        if (successExitCodes != null) tool["SuccessExitCodes"] = successExitCodes;
        _cliTools.Add(tool);
        return this;
    }

    /// <summary>
    /// Sets the UseCliToolAlias at the ProductDefaults level, cascading to all targets.
    /// </summary>
    public ScenarioBuilder WithUseCliToolAlias(string alias)
    {
        _useCliToolAlias = alias;
        return this;
    }

    /// <summary>
    /// Sets CliToolParameters on all targets for placeholder substitution in ArgumentTemplate.
    /// </summary>
    public ScenarioBuilder WithCliToolParameters(Dictionary<string, string> parameters)
    {
        _targetCliToolParameters = parameters;
        return this;
    }

    /// <summary>
    /// Converts a release from traditional layout (Backend/ subdirectory) to flat layout
    /// by moving all files directly under the release directory.
    /// Only valid for single-TargetGroup scenarios.
    /// </summary>
    public ScenarioBuilder WithFlatLayoutForRelease(string release)
    {
        _fileMutations.Add(workDir =>
        {
            string tgDir = Path.Combine(workDir, release, "Backend");
            if (!Directory.Exists(tgDir)) return;

            string releaseDir = Path.Combine(workDir, release);
            foreach (var file in Directory.GetFiles(tgDir))
            {
                string destPath = Path.Combine(releaseDir, Path.GetFileName(file));
                File.Move(file, destPath);
            }
            Directory.Delete(tgDir, recursive: true);
        });
        return this;
    }

    /// <summary>
    /// Sets TargetGroupMigrationOrder at the product level in appsettings (comma-separated string).
    /// Mirrors the ProductOptions.TargetGroupMigrationOrder configuration field.
    /// </summary>
    public ScenarioBuilder WithTargetGroupMigrationOrder(string commaSeparated)
    {
        _targetGroupMigrationOrder = commaSeparated;
        return this;
    }

    /// <summary>
    /// Enables database logging with the specified minimum log level.
    /// </summary>
    public ScenarioBuilder WithDatabaseLogging(string? minimumLevel = "Debug")
    {
        _databaseLogging = true;
        _databaseLoggingMinLevel = minimumLevel ?? "Debug";
        return this;
    }

    /// <summary>
    /// Sets the DbCommandMaxRetries and DbCommandWaitTimeInMsBeforeRetry for target execution.
    /// </summary>
    public ScenarioBuilder WithTargetMaxRetries(int maxRetries, int retryDelayMs = 250)
    {
        _targetMaxRetries = maxRetries;
        _targetRetryDelayMs = retryDelayMs;
        return this;
    }

    /// <summary>
    /// Sets the DbCommandTimeoutInSeconds for target execution.
    /// </summary>
    public ScenarioBuilder WithTargetCommandTimeout(int seconds)
    {
        _targetCommandTimeoutSeconds = seconds;
        return this;
    }

    /// <summary>
    /// Builds the scenario: copies migration files to a temp directory, applies mutations,
    /// generates configuration, cleans databases, creates DI container, and returns a
    /// ScenarioContext ready for execution.
    /// </summary>
    public async Task<ScenarioContext> BuildAsync(
        MigrationCommand command = MigrationCommand.MigrateUp,
        MigrationRunMode mode = MigrationRunMode.Migrate,
        string? toRelease = null)
    {
        // 1. Create temp directory
        string workDir = Path.Combine(Path.GetTempPath(), "RayMigrator_EngineTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        // 2. Copy base files from _engineConfig.BaseFilesPath to workDir
        CopyDirectory(_engineConfig.BaseFilesPath, workDir);

        // 3. Apply file mutations
        foreach (var mutation in _fileMutations)
            mutation(workDir);

        // 4. Generate JSON config file
        string configPath = Path.Combine(workDir, "appsettings.json");
        const string productAlias = "EngineTest";
        GenerateConfig(configPath, workDir, productAlias);

        // 5. Clean databases
        DatabaseCleanupHelper.CleanDatabase(_engineConfig.DatabaseType, _engineConfig.ConnectionString, _engineConfig.SchemaName);
        if (_secondConnectionString != null)
            DatabaseCleanupHelper.CleanDatabase(_engineConfig.DatabaseType, _secondConnectionString, _engineConfig.SchemaName);

        foreach (var (_, dbType, connStr, _, _) in _additionalTargetGroups)
            DatabaseCleanupHelper.CleanDatabase(dbType, connStr, _engineConfig.SchemaName);

        // 6. Build host
        var host = new EngineTestHost();
        host.Build(configPath, productAlias, command, mode, toRelease);

        // 7. Create query helper
        var queryHelper = new RepositoryQueryHelper(_engineConfig.DatabaseType, _engineConfig.ConnectionString, _engineConfig.SchemaName);

        // 8. Return context
        return new ScenarioContext(host, queryHelper, _engineConfig, workDir, configPath, productAlias);
    }

    /// <summary>
    /// Generates the appsettings.json configuration file for the test scenario.
    /// Uses System.Text.Json to avoid path escaping issues.
    /// </summary>
    private void GenerateConfig(string configPath, string workDir, string productAlias)
    {
        // Build targets list
        var mainTarget = new Dictionary<string, object>
        {
            ["Alias"] = "MainDB",
            ["ConnectionString"] = _engineConfig.ConnectionString
        };
        if (_targetCliToolParameters != null)
            mainTarget["CliToolParameters"] = _targetCliToolParameters;

        var targets = new List<Dictionary<string, object>> { mainTarget };

        if (_secondConnectionString != null)
        {
            var secondTarget = new Dictionary<string, object>
            {
                ["Alias"] = "SecondDB",
                ["ConnectionString"] = _secondConnectionString
            };
            if (_targetCliToolParameters != null)
                secondTarget["CliToolParameters"] = _targetCliToolParameters;
            targets.Add(secondTarget);
        }

        // Build TargetDefaults
        var targetDefaults = new Dictionary<string, object>
        {
            ["DbCommandTimeoutInSeconds"] = _targetCommandTimeoutSeconds ?? 30,
            ["DbCommandMaxRetries"] = _targetMaxRetries ?? 0,
            ["DbCommandWaitTimeInMsBeforeRetry"] = _targetRetryDelayMs ?? 250
        };

        // Build TargetGroupDefaults
        var targetGroupDefaults = new Dictionary<string, object>
        {
            ["TargetDefaults"] = targetDefaults,
            ["HashValidationScope"] = "File"
        };

        if (_targetMigrationOrder.HasValue)
            targetGroupDefaults["TargetMigrationOrder"] = _targetMigrationOrder.Value.ToString();
        else
            targetGroupDefaults["TargetMigrationOrder"] = TargetMigrationOrder.Simultaneously.ToString();

        // Build ProductDefaults
        var productDefaults = new Dictionary<string, object>
        {
            ["MigrationErrorAction"] = (_migrationErrorAction ?? MigrationErrorAction.Terminate).ToString(),
            ["MigrationFilesExtension"] = "sql",
            ["MigrationRollbackFilesPreExtension"] = "rollback",
            ["MigrationFilesEncoding"] = "UTF-8",
            ["RequireRollbackFile"] = _requireRollbackFile ?? true,
            ["TargetGroupDefaults"] = targetGroupDefaults
        };

        if (_stopRollbackOnMissingRollbackFile.HasValue)
            productDefaults["StopRollbackOnMissingRollbackFile"] = _stopRollbackOnMissingRollbackFile.Value;

        // RULE_2_11: Rollback-class MigrationErrorAction requires a RollbackErrorAction to be set.
        // Default to Terminate when the test did not specify one explicitly, so fixtures exercising
        // the rollback flow don't fail config validation at host.Build().
        var effectiveRollbackErrorAction = _rollbackErrorAction;
        if (!effectiveRollbackErrorAction.HasValue)
        {
            var errorAction = _migrationErrorAction ?? MigrationErrorAction.Terminate;
            if (errorAction is MigrationErrorAction.Rollback
                or MigrationErrorAction.RollbackErrorOnly
                or MigrationErrorAction.RollbackRelease)
            {
                effectiveRollbackErrorAction = RollbackErrorAction.Terminate;
            }
        }
        if (effectiveRollbackErrorAction.HasValue)
            productDefaults["RollbackErrorAction"] = effectiveRollbackErrorAction.Value.ToString();

        if (_useCliToolAlias != null)
            productDefaults["UseCliToolAlias"] = _useCliToolAlias;

        // Build TargetGroups list
        var targetGroups = new List<Dictionary<string, object>>
        {
            new()
            {
                ["Alias"] = "Backend",
                ["DatabaseType"] = _engineConfig.DatabaseType,
                ["Targets"] = targets
            }
        };

        // Add additional target groups
        foreach (var (alias, dbType, connStr, order, hashScope) in _additionalTargetGroups)
        {
            var tg = new Dictionary<string, object>
            {
                ["Alias"] = alias,
                ["DatabaseType"] = dbType,
                ["Targets"] = new List<Dictionary<string, object>>
                {
                    new() { ["Alias"] = $"{alias}DB", ["ConnectionString"] = connStr }
                }
            };
            if (order.HasValue) tg["TargetMigrationOrder"] = order.Value.ToString();
            if (hashScope.HasValue) tg["HashValidationScope"] = hashScope.Value.ToString();
            targetGroups.Add(tg);
        }

        // Build Products
        var product = new Dictionary<string, object>
        {
            ["Alias"] = productAlias,
            ["MigrationFilesRootDirectory"] = workDir,
            ["TargetGroups"] = targetGroups
        };

        if (_targetGroupMigrationOrder != null)
            product["TargetGroupMigrationOrder"] = _targetGroupMigrationOrder;

        // Build Repository
        var repository = new Dictionary<string, object>
        {
            ["DatabaseType"] = _engineConfig.DatabaseType,
            ["ConnectionString"] = _engineConfig.ConnectionString,
            ["SchemaName"] = _engineConfig.SchemaName,
            ["TableBaseName"] = "",
            ["DbCommandTimeoutInSeconds"] = 60,
            ["DbCommandMaxRetries"] = 100,
            ["DbCommandWaitTimeInMsBeforeRetry"] = 250
        };

        // Build Serilog
        var serilog = new Dictionary<string, object>
        {
            ["MinimumLevel"] = new Dictionary<string, object>
            {
                ["Default"] = "Warning"
            },
            ["WriteTo"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "Console" }
            }
        };

        // Assemble full config
        var rayMigratorConfig = new Dictionary<string, object>
        {
            ["Repository"] = repository,
            ["ProductDefaults"] = productDefaults,
            ["Products"] = new List<Dictionary<string, object>> { product },
            ["Serilog"] = serilog
        };

        // Add CliTools section if configured
        if (_cliTools.Count > 0)
            rayMigratorConfig["CliTools"] = _cliTools;

        // Add DatabaseLogging section if enabled
        if (_databaseLogging)
        {
            rayMigratorConfig["DatabaseLogging"] = new Dictionary<string, object>
            {
                ["DatabaseType"] = _engineConfig.DatabaseType,
                ["ConnectionString"] = _engineConfig.ConnectionString,
                ["SchemaName"] = _engineConfig.SchemaName,
                ["TableBaseName"] = "",
                ["MinimumLevel"] = _databaseLoggingMinLevel,
                ["DbCommandTimeoutInSeconds"] = 20
            };
        }

        var config = new Dictionary<string, object>
        {
            ["RayMigrator"] = rayMigratorConfig
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        string json = JsonSerializer.Serialize(config, jsonOptions);
        File.WriteAllText(configPath, json);
    }

    /// <summary>
    /// Recursively copies all files and directories from source to destination.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));

        foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            File.Copy(filePath, filePath.Replace(sourceDir, destDir), true);
    }

    /// <summary>
    /// Extracts the TOML header and SQL body from a migration file's content.
    /// </summary>
    private static (string toml, string sql) ExtractTomlAndSql(string content)
    {
        int tomlStart = content.IndexOf("/*", StringComparison.Ordinal);
        if (tomlStart < 0) return ("", content);

        int tomlEnd = content.IndexOf("*/", tomlStart, StringComparison.Ordinal);
        if (tomlEnd < 0) return ("", content);

        string toml = content.Substring(tomlStart, tomlEnd + 2 - tomlStart);
        string sql = content.Substring(tomlEnd + 2).Trim();
        return (toml, sql);
    }

    /// <summary>
    /// Converts a migration filename to its corresponding rollback filename.
    /// Example: "01_CreateTable.sql" becomes "01_CreateTable.rollback.sql"
    /// </summary>
    private static string GetRollbackFilename(string filename)
    {
        if (!filename.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Expected .sql extension but got: '{filename}'", nameof(filename));

        return filename.Substring(0, filename.Length - 4) + ".rollback.sql";
    }
}
