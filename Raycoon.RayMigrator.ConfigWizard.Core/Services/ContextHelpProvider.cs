// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Globalization;
using System.Resources;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Provides multilingual context help for wizard sections and fields using .resx resources.
/// </summary>
public static class ContextHelpProvider
{
    private static readonly ResourceManager SectionHelpResources =
        new("Raycoon.RayMigrator.ConfigWizard.Core.Resources.SectionHelp",
            typeof(ContextHelpProvider).Assembly);

    private static readonly ResourceManager FieldHelpResources =
        new("Raycoon.RayMigrator.ConfigWizard.Core.Resources.FieldHelp",
            typeof(ContextHelpProvider).Assembly);

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
    /// Returns section-level help for a wizard step.
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
    /// Returns field-level help for a specific field.
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
    /// Returns all section help keys (for completeness checks in tests).
    /// </summary>
    public static IReadOnlyList<string> GetAllSectionKeys() => SectionKeys;

    /// <summary>
    /// Returns all field help keys (for completeness checks in tests).
    /// </summary>
    public static IReadOnlyList<string> GetAllFieldKeys() => FieldKeys;
}
