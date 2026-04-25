using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Language-independent registry of JSON configuration paths for wizard field keys.
/// All paths are relative to the RayMigrator root section.
/// </summary>
public static class JsonPathRegistry
{
    private static readonly Dictionary<string, JsonPathInfo> Registry = new()
    {
        // ── Repository (no inheritance) ──────────────────────────
        ["Repository_DatabaseType"] = new("Repository.DatabaseType"),
        ["Repository_ConnectionString"] = new("Repository.ConnectionString"),
        ["Repository_SchemaName"] = new("Repository.SchemaName"),
        ["Repository_TableBaseName"] = new("Repository.TableBaseName"),
        ["Repository_Timeout"] = new("Repository.DbCommandTimeoutInSeconds"),
        ["Repository_MaxRetries"] = new("Repository.DbCommandMaxRetries"),
        ["Repository_Wait"] = new("Repository.DbCommandWaitTimeInMsBeforeRetry"),

        // ── DatabaseLogging (no inheritance) ─────────────────────
        ["DatabaseLogging_Enable"] = new("DatabaseLogging"),
        ["DatabaseLogging_DatabaseType"] = new("DatabaseLogging.DatabaseType"),
        ["DatabaseLogging_ConnectionString"] = new("DatabaseLogging.ConnectionString"),
        ["DatabaseLogging_SchemaName"] = new("DatabaseLogging.SchemaName"),
        ["DatabaseLogging_MinimumLevel"] = new("DatabaseLogging.MinimumLevel"),
        ["DatabaseLogging_Timeout"] = new("DatabaseLogging.DbCommandTimeoutInSeconds"),
        ["DatabaseLogging_TableBaseName"] = new("DatabaseLogging.TableBaseName"),

        // ── ProductDefaults (inherited BY Products[]) ────────────
        ["ProductDefaults_MigrationErrorAction"] = new(
            "ProductDefaults.MigrationErrorAction",
            InheritedByPaths: ["Products[].MigrationErrorAction"]),
        ["ProductDefaults_RollbackErrorAction"] = new(
            "ProductDefaults.RollbackErrorAction",
            InheritedByPaths: ["Products[].RollbackErrorAction"]),
        ["ProductDefaults_MigrationFilesExtension"] = new(
            "ProductDefaults.MigrationFilesExtension",
            InheritedByPaths: ["Products[].MigrationFilesExtension"]),
        ["ProductDefaults_RollbackFilesPreExtension"] = new(
            "ProductDefaults.MigrationRollbackFilesPreExtension",
            InheritedByPaths: ["Products[].MigrationRollbackFilesPreExtension"]),
        ["ProductDefaults_MigrationFilesEncoding"] = new(
            "ProductDefaults.MigrationFilesEncoding",
            InheritedByPaths: ["Products[].MigrationFilesEncoding"]),
        ["ProductDefaults_RequireRollbackFile"] = new(
            "ProductDefaults.RequireRollbackFile",
            InheritedByPaths: ["Products[].RequireRollbackFile"]),
        ["ProductDefaults_StopRollbackOnMissingRollbackFile"] = new(
            "ProductDefaults.StopRollbackOnMissingRollbackFile",
            InheritedByPaths: ["Products[].StopRollbackOnMissingRollbackFile"]),
        ["ProductDefaults_UseCliToolAlias"] = new(
            "ProductDefaults.UseCliToolAlias",
            InheritedByPaths: ["Products[].UseCliToolAlias"]),

        // ── ProductDefaults.TargetGroupDefaults (inherited BY Products[].TargetGroups[]) ──
        ["ProductDefaults_TargetMigrationOrder"] = new(
            "ProductDefaults.TargetGroupDefaults.TargetMigrationOrder",
            InheritedByPaths: ["Products[].TargetGroups[].TargetMigrationOrder"]),
        ["ProductDefaults_HashValidationScope"] = new(
            "ProductDefaults.TargetGroupDefaults.HashValidationScope",
            InheritedByPaths: ["Products[].TargetGroups[].HashValidationScope"]),

        // ── Product (no inheritance metadata — shared FieldKey with ProductDefaults) ──
        ["Product_Alias"] = new("Products[].Alias"),
        ["Product_MigrationFilesRootDirectory"] = new("Products[].MigrationFilesRootDirectory"),
        ["Product_UseCliToolAlias"] = new("Products[].UseCliToolAlias"),
        ["Product_TargetGroupMigrationOrder"] = new("Products[].TargetGroupMigrationOrder"),

        // ── TargetGroup ──────────────────────────────────────────
        ["TargetGroup_Alias"] = new("Products[].TargetGroups[].Alias"),
        ["TargetGroup_DatabaseType"] = new("Products[].TargetGroups[].DatabaseType"),
        ["TargetGroup_TargetMigrationOrder"] = new(
            "Products[].TargetGroups[].TargetMigrationOrder",
            InheritedFromPath: "ProductDefaults.TargetGroupDefaults.TargetMigrationOrder"),
        ["TargetGroup_HashValidationScope"] = new(
            "Products[].TargetGroups[].HashValidationScope",
            InheritedFromPath: "ProductDefaults.TargetGroupDefaults.HashValidationScope"),
        ["TargetGroup_UseCliToolAlias"] = new(
            "Products[].TargetGroups[].UseCliToolAlias",
            InheritedFromPath: "Products[].UseCliToolAlias"),

        // ── Target ───────────────────────────────────────────────
        ["Target_Alias"] = new("Products[].TargetGroups[].Targets[].Alias"),
        ["Target_ConnectionString"] = new("Products[].TargetGroups[].Targets[].ConnectionString"),
        ["Target_Timeout"] = new(
            "Products[].TargetGroups[].Targets[].DbCommandTimeoutInSeconds",
            InheritedFromPath: "ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds"),
        ["Target_MaxRetries"] = new(
            "Products[].TargetGroups[].Targets[].DbCommandMaxRetries",
            InheritedFromPath: "ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries"),
        ["Target_Wait"] = new(
            "Products[].TargetGroups[].Targets[].DbCommandWaitTimeInMsBeforeRetry",
            InheritedFromPath: "ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry"),
        ["Target_UseCliToolAlias"] = new(
            "Products[].TargetGroups[].Targets[].UseCliToolAlias",
            InheritedFromPath: "Products[].TargetGroups[].UseCliToolAlias"),
        ["Target_CliToolParameters"] = new("Products[].TargetGroups[].Targets[].CliToolParameters"),

        // ── CliTools (no inheritance) ────────────────────────────
        ["CliTool_Alias"] = new("CliTools[].Alias"),
        ["CliTool_ExecutablePath"] = new("CliTools[].ExecutablePath"),
        ["CliTool_ArgumentTemplate"] = new("CliTools[].ArgumentTemplate"),
        ["CliTool_InputMode"] = new("CliTools[].InputMode"),
        ["CliTool_SuccessExitCodes"] = new("CliTools[].SuccessExitCodes"),
        ["CliTool_Timeout"] = new("CliTools[].CliToolTimeoutInSeconds"),

        // ── Serilog (no inheritance) ─────────────────────────────
        ["Serilog_MinimumLevel"] = new("Serilog.MinimumLevel.Default"),
        ["Serilog_SinkName"] = new("Serilog.WriteTo[].Name"),
        ["Serilog_OverrideSource"] = new("Serilog.MinimumLevel.Override"),
        ["Serilog_OverrideLevel"] = new("Serilog.MinimumLevel.Override.<Source>"),
        ["Serilog_SinkArgs"] = new("Serilog.WriteTo[].Args"),

        // ── Concept Help ─────────────────────────────────────────
        // Concept_Environment, Concept_TargetGroup, Concept_Target
        // have no JSON path — intentionally not registered.
    };

    /// <summary>
    /// Returns path metadata for a field key, or null if the field has no JSON path.
    /// </summary>
    public static JsonPathInfo? GetPathInfo(string fieldKey) =>
        Registry.GetValueOrDefault(fieldKey);
}
