using System.Globalization;
using System.Resources;
namespace Raycoon.RayMigrator.Shared.Configuration.Help;

/// <summary>
/// Help texts for the sections and fields of the RayMigrator configuration in English and German, read from the
/// .resx resources of this assembly. Field keys use underscores (<c>Repository_DatabaseType</c>); the
/// <see cref="JsonPathRegistry"/> maps them to configuration paths. Shared by the Config Wizard and Studio.
/// </summary>
public static class ConfigurationHelp
{
    private static readonly ResourceManager SectionHelpResources =
        new("Raycoon.RayMigrator.Shared.Configuration.Help.SectionHelp",
            typeof(ConfigurationHelp).Assembly);

    private static readonly ResourceManager FieldHelpResources =
        new("Raycoon.RayMigrator.Shared.Configuration.Help.FieldHelp",
            typeof(ConfigurationHelp).Assembly);

    private static readonly string[] SectionKeys =
    {
        "Welcome", "Root", "Repository", "DatabaseLogging", "ProductDefaults",
        "Product", "TargetGroup", "Target", "Serilog", "CliTools",
        "Products", "TargetGroups", "Targets"
    };

    private static readonly string[] FieldKeys =
    {
        // Repository
        "Repository_DatabaseType", "Repository_ConnectionString", "Repository_SchemaName",
        "Repository_TableBaseName", "Repository_Timeout", "Repository_MaxRetries", "Repository_Wait",
        "Repository_HeartbeatIntervalInSeconds", "Repository_OrphanedRunThresholdInMinutes",
        // DatabaseLogging
        "DatabaseLogging_Enable", "DatabaseLogging_DatabaseType", "DatabaseLogging_ConnectionString",
        "DatabaseLogging_SchemaName", "DatabaseLogging_MinimumLevel", "DatabaseLogging_Timeout",
        "DatabaseLogging_TableBaseName",
        // ProductDefaults
        "ProductDefaults_MigrationErrorAction", "ProductDefaults_RollbackErrorAction",
        "ProductDefaults_MigrationFilesExtension", "ProductDefaults_RollbackFilesPreExtension",
        "ProductDefaults_MigrationFilesEncoding", "ProductDefaults_RequireRollbackFile",
        "ProductDefaults_StopRollbackOnMissingRollbackFile",
        "ProductDefaults_TargetMigrationOrder", "ProductDefaults_HashValidationScope",
        "ProductDefaults_UseCliToolAlias",
        // Product
        "Product_Alias", "Product_MigrationFilesRootDirectory", "Product_UseCliToolAlias",
        "Product_TargetGroupMigrationOrder",
        // TargetGroup
        "TargetGroup_Alias", "TargetGroup_DatabaseType", "TargetGroup_TargetMigrationOrder",
        "TargetGroup_HashValidationScope", "TargetGroup_UseCliToolAlias",
        // Target
        "Target_Alias", "Target_ConnectionString", "Target_Timeout", "Target_MaxRetries",
        "Target_Wait", "Target_UseCliToolAlias", "Target_CliToolParameters",
        // CliTool
        "CliTool_Alias", "CliTool_ExecutablePath",
        "CliTool_ArgumentTemplate", "CliTool_InputMode", "CliTool_SuccessExitCodes",
        "CliTool_Timeout",
        // Serilog
        "Serilog_MinimumLevel", "Serilog_SinkName",
        "Serilog_OverrideSource", "Serilog_OverrideLevel", "Serilog_SinkArgs",
        // Concept Help
        "Concept_Environment", "Concept_TargetGroup", "Concept_Target",
    };

    /// <summary>
    /// Returns the help of a section, or null when the key is unknown.
    /// </summary>
    public static SectionHelp? GetSectionHelp(string sectionKey, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;

        var title = SectionHelpResources.GetString($"{sectionKey}_Title", culture);
        var description = SectionHelpResources.GetString($"{sectionKey}_Description", culture);

        if (title == null && description == null)
            return null;

        return new SectionHelp(title ?? sectionKey, description ?? "");
    }

    /// <summary>
    /// Returns the help of a field, or null when the key is unknown.
    /// The fieldKey uses underscore as separator (e.g. "Repository_DatabaseType").
    /// </summary>
    public static FieldHelp? GetFieldHelp(string fieldKey, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;

        var title = FieldHelpResources.GetString($"{fieldKey}_Title", culture);
        var description = FieldHelpResources.GetString($"{fieldKey}_Description", culture);

        if (title == null && description == null)
            return null;

        return new FieldHelp(
            title ?? fieldKey,
            description ?? "",
            FieldHelpResources.GetString($"{fieldKey}_Examples", culture),
            FieldHelpResources.GetString($"{fieldKey}_ValidValues", culture),
            FieldHelpResources.GetString($"{fieldKey}_DefaultValue", culture),
            FieldHelpResources.GetString($"{fieldKey}_InheritanceNote", culture),
            JsonPathRegistry.GetPathInfo(fieldKey)
        );
    }

    /// <summary>
    /// Returns all section help keys.
    /// </summary>
    public static IReadOnlyList<string> GetAllSectionKeys() => SectionKeys;

    /// <summary>
    /// Returns all field help keys.
    /// </summary>
    public static IReadOnlyList<string> GetAllFieldKeys() => FieldKeys;
}
