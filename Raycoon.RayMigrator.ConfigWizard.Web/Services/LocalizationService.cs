// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Globalization;
using Microsoft.JSInterop;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;

namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>
/// Manages UI language selection and provides localized strings.
/// Wraps Core's ContextHelpProvider for section/field help and adds Web-specific UI strings.
/// </summary>
public class LocalizationService
{
    private const string StorageKey = "rayMigratorLanguage";

    private readonly IJSRuntime? _jsRuntime;
    private string _language = "en";
    private bool _initialized;

    public record LanguageInfo(string Code, string NativeName, string CultureName);

    public static readonly IReadOnlyList<LanguageInfo> SupportedLanguages =
    [
        new("en", "English", "en-US"),
        new("de", "Deutsch", "de-DE")
    ];

    private static readonly Dictionary<string, string> CultureMap =
        SupportedLanguages.ToDictionary(l => l.Code, l => l.CultureName);

    public LocalizationService(IJSRuntime? jsRuntime = null)
    {
        _jsRuntime = jsRuntime;
    }

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            PersistLanguage(value);
            LanguageChanged?.Invoke();
        }
    }

    public CultureInfo Culture => CultureMap.TryGetValue(_language, out var name)
        ? CultureInfo.GetCultureInfo(name)
        : CultureInfo.GetCultureInfo("en-US");

    public event Action? LanguageChanged;

    /// <summary>
    /// Synchronous initialization — reads language from localStorage BEFORE first render.
    /// Must be called from Program.cs before app.RunAsync().
    /// Uses IJSInProcessRuntime (available in Blazor WASM).
    /// </summary>
    public void InitializeSync()
    {
        if (_initialized) return;
        _initialized = true;

        if (_jsRuntime is IJSInProcessRuntime jsInProcess)
        {
            try
            {
                var stored = jsInProcess.Invoke<string?>("getLocalStorage", StorageKey);
                if (stored is not null && CultureMap.ContainsKey(stored))
                {
                    _language = stored;
                }
            }
            catch
            {
                // localStorage may be unavailable
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized || _jsRuntime is null) return;
        _initialized = true;

        try
        {
            var stored = await _jsRuntime.InvokeAsync<string?>("getLocalStorage", StorageKey);
            if (stored is not null && CultureMap.ContainsKey(stored))
            {
                _language = stored;
            }
        }
        catch
        {
            // localStorage may be unavailable (private browsing, SSR) — silently use default
        }
    }

    private void PersistLanguage(string code)
    {
        if (_jsRuntime is null) return;

        // Fire-and-forget — language persistence is best-effort
        _ = Task.Run(async () =>
        {
            try { await _jsRuntime.InvokeVoidAsync("setLocalStorage", StorageKey, code); }
            catch { /* localStorage unavailable */ }
        });
    }

    // Core help provider wrappers
    public SectionHelp? GetSectionHelp(string sectionKey) =>
        ContextHelpProvider.GetSectionHelp(sectionKey, Culture);

    public FieldHelp? GetFieldHelp(string fieldKey) =>
        ContextHelpProvider.GetFieldHelp(fieldKey, Culture);

    // Web UI strings
    public string Get(string key)
    {
        if (Translations.TryGetValue(_language, out var dict) && dict.TryGetValue(key, out var val))
            return val;
        // Fallback to English
        if (Translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
            return enVal;
        return key;
    }

    // ── Translations ─────────────────────────────────────────────────

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["Welcome.Title"] = "Welcome to RayMigrator Config Wizard",
            ["Welcome.Subtitle"] = "Create or import your RayMigrator configuration",
            ["Welcome.CreateNew"] = "Create New Configuration",
            ["Welcome.CreateNewDescription"] = "Start from scratch with the guided setup wizard.",
            ["Welcome.UploadExisting"] = "Edit Existing Configuration",
            ["Welcome.UploadDescription"] = "Load existing appsettings*.json files from your computer to review and edit them.",
            ["Welcome.SelectFiles"] = "Select Files",
            ["Welcome.UploadPrivacy"] = "Your files stay on your computer and are processed only locally in the browser.",
            ["Welcome.CreatePrivacy"] = "Your data stays on your computer and is processed only locally in the browser.",
            ["Welcome.GoToOverview"] = "Go to Overview",
            ["Welcome.WalkThrough"] = "Walk Through Wizard",
            ["Welcome.Language"] = "Language",

            ["Repository.Title"] = "Repository Database",
            ["Repository.Subtitle"] = "Choose the database engine for the migration metadata repository.",
            ["Repository.TableBaseName"] = "Table Base Name",

            ["Products.Title"] = "Products",
            ["Products.Subtitle"] = "Define the products that RayMigrator will manage.",
            ["Products.AddProduct"] = "Add Product",
            ["Products.ProductAlias"] = "Product Alias",
            ["Products.RemoveProduct"] = "Remove",
            ["Products.Empty"] = "No products defined. Add at least one product.",
            ["Products.MigrationFilesRootDirectory"] = "Migration Files Root Directory",

            ["ProductDetail.Title"] = "Product Details",
            ["ProductDetail.Environments"] = "Environments",
            ["ProductDetail.AddEnvironment"] = "Add Environment",
            ["ProductDetail.TargetGroups"] = "Target Groups",
            ["ProductDetail.AddTargetGroup"] = "Add Target Group",
            ["ProductDetail.TargetGroupAlias"] = "Target Group Alias",
            ["ProductDetail.DatabaseType"] = "Database Type",
            ["ProductDetail.Targets"] = "Targets",
            ["ProductDetail.AddTarget"] = "Add Target",
            ["ProductDetail.TargetAlias"] = "Target Alias",
            ["ProductDetail.RemoveTarget"] = "Remove",
            ["ProductDetail.RemoveTargetGroup"] = "Remove",
            ["ProductDetail.RemoveEnvironment"] = "Remove",

            ["OptionalFeatures.Title"] = "Optional Features",
            ["OptionalFeatures.Subtitle"] = "Configure optional features for your setup.",
            ["OptionalFeatures.DatabaseLogging"] = "Database Logging",
            ["OptionalFeatures.DatabaseLoggingHint"] = "Strongly recommended. Disabling means no migration logs in the database.",
            ["OptionalFeatures.CliTools"] = "CLI Tools",
            ["OptionalFeatures.CliToolsHint"] = "Enable if you need to execute migrations via external CLI tools (sqlcmd, psql, etc.).",

            ["Summary.Title"] = "Setup Summary",
            ["Summary.Subtitle"] = "Review your setup before proceeding to detailed configuration.",
            ["Summary.Confirm"] = "Confirm and Continue",
            ["Summary.Edit"] = "Edit Setup",
            ["Summary.Restart"] = "Restart Setup",
            ["Summary.Repository"] = "Repository",
            ["Summary.DatabaseLogging"] = "Database Logging",
            ["Summary.CliTools"] = "CLI Tools",
            ["Summary.Enabled"] = "Enabled",
            ["Summary.Disabled"] = "Disabled",

            ["Phase2.Title"] = "Detailed Configuration",
            ["Phase2.Subtitle"] = "Configure each section in detail. Use the navigation to move between sections.",
            ["Phase2.BackToStart"] = "Back to Start",
            ["Phase2.Completed"] = "All sections have been configured.",
            ["Phase2.GoToOverview"] = "Continue to Overview",

            ["Phase3.Title"] = "Configuration Overview",
            ["Phase3.Subtitle"] = "Review and edit all sections. Download your configuration files when ready.",
            ["Phase3.Download"] = "Download ZIP",

            ["Section.Repository"] = "Repository",
            ["Section.DatabaseLogging"] = "Database Logging",
            ["Section.CliTools"] = "CLI Tools",
            ["Section.ProductDefaults"] = "Product Defaults",
            ["Section.ProductSettings"] = "Product Settings",
            ["Section.Serilog"] = "Serilog",

            ["Common.Next"] = "Next",
            ["Common.Back"] = "Back",
            ["Common.Save"] = "Save",
            ["Common.Cancel"] = "Cancel",
            ["Common.Add"] = "Add",
            ["Common.Remove"] = "Remove",
            ["Common.Edit"] = "Edit",
            ["Common.Alias"] = "Alias",
            ["Common.ConnectionString"] = "Connection String",
            ["Common.DatabaseType"] = "Database Type",
            ["Common.SchemaName"] = "Schema Name",
            ["Common.Errors"] = "Errors",
            ["Common.Warnings"] = "Warnings",
            ["Common.Valid"] = "Valid",
            ["Common.InheritedFrom"] = "Inherited from",
            ["Common.Override"] = "Override",
            ["Common.Default"] = "Default",
            ["Common.Timeout"] = "Command Timeout (s)",
            ["Common.MaxRetries"] = "Max Retries",
            ["Common.WaitTime"] = "Wait Time (ms)",

            ["Env.Tabs"] = "Configuration Files",
            ["Env.Base"] = "Base (appsettings.json)",

            ["Promotion.Applied"] = "Defaults promoted successfully",

            ["JsonPreview.Title"] = "JSON Preview",
            ["Export.Success"] = "Configuration exported successfully.",

            ["Matrix.Title"] = "Product / Environment Matrix",
            ["Matrix.Empty"] = "No products or environments defined yet.",
            ["Matrix.Configured"] = "Configured",
            ["Matrix.BaseOnly"] = "Base only",
            ["Matrix.Product"] = "Product",

            ["DbLogging.Disabled"] = "Database Logging is disabled. Enable it in the setup phase to configure.",
            ["DbLogging.Recommendation"] = "Database logging is strongly recommended. It provides a complete audit trail of all migration runs, including which scripts were executed, their results, and any errors that occurred. Without database logging, this information is only available in console output or file logs, which may be lost after the session ends. In production environments, database logging is essential for troubleshooting failed migrations and verifying deployment history.",
            ["DbLogging.TableBaseName"] = "Table Base Name",
            ["DbLogging.MinimumLevel"] = "Minimum Level",

            ["CliTools.Empty"] = "No CLI tools configured. Add tools to execute migrations via external CLI tools.",
            ["CliTools.ExecutablePath"] = "Executable Path",
            ["CliTools.ArgumentTemplate"] = "Argument Template",
            ["CliTools.InputMode"] = "Input Mode",
            ["CliTools.SuccessExitCodes"] = "Success Exit Codes",
            ["CliTools.SuccessExitCodesHelp"] = "Comma-separated. Examples: 0  or  0, 1..5  or  0, 10..",
            ["CliTools.AddTool"] = "Add CLI Tool",

            ["CliParams.DialogTitle"] = "CLI Tool Parameters",
            ["CliParams.TemplateReference"] = "Argument Template",
            ["CliParams.Required"] = "This parameter is required",
            ["CliParams.NoParametersFound"] = "No parameters found in the argument template.",
            ["CliParams.SetParameters"] = "Set Parameters",
            ["CliParams.EditParameters"] = "Edit Parameters",
            ["CliParams.ClearParameters"] = "Clear Parameters",
            ["CliParams.InheritedFrom"] = "Inherited from",

            ["ProductDefaults.MigrationErrorAction"] = "Migration Error Action",
            ["ProductDefaults.RollbackErrorAction"] = "Rollback Error Action",
            ["ProductDefaults.MigrationFilesExtension"] = "Migration Files Extension",
            ["ProductDefaults.RollbackPreExtension"] = "Rollback Pre-Extension",
            ["ProductDefaults.MigrationFilesEncoding"] = "Migration Files Encoding",
            ["ProductDefaults.RequireRollbackFile"] = "Require Rollback File",
            ["ProductDefaults.StopRollbackOnMissingRollbackFile"] = "Stop Rollback on Missing Rollback File",
            ["ProductDefaults.TgStopRollbackOnMissingRollbackFile"] = "Stop Rollback on Missing Rollback File (TargetGroup Default)",
            ["ProductDefaults.TargetGroupDefaultsTitle"] = "Target Group Defaults",
            ["ProductDefaults.TargetMigrationOrder"] = "Target Migration Order",
            ["ProductDefaults.HashValidationScope"] = "Hash Validation Scope",
            ["ProductDefaults.TargetDefaultsTitle"] = "Target Defaults",
            ["ProductDefaults.DefaultCliToolAlias"] = "Use CLI Tool",

            ["Products.TargetGroupMigrationOrder"] = "Target Group Execution Order",
            ["Products.TargetGroupMigrationOrderHint"] = "Comma-separated list of target group aliases (e.g. Backend,Frontend)",

            ["Serilog.SinkName"] = "Sink Name",
            ["Serilog.LevelOverrides"] = "Level Overrides",
            ["Serilog.MinimumLevel"] = "Minimum Level",
            ["Serilog.AddSink"] = "Add Sink",

            ["Upload.FilesLoaded"] = "file(s) loaded",

            ["Help.ValidValues"] = "Valid Values",
            ["Help.Default"] = "Default",
            ["Help.Examples"] = "Examples",
            ["Help.JsonConfigPath"] = "JSON Config Path",
            ["Help.InheritedBy"] = "Inherited by",
            ["Common.Close"] = "Close",

            ["DbLogging.Enable"] = "Enable Database Logging",

            ["Products.OverrideProductDefaults"] = "Override Product Defaults",
            ["Products.OverrideTargetGroupDefaults"] = "Override Target Group Defaults",
            ["Products.OverrideTargetDefaults"] = "Override Target Defaults",
            ["Products.AddTargetBtn"] = "Add Target",
            ["Products.AddTargetGroupBtn"] = "Add Target Group",
            ["Products.AddProductBtn"] = "Add Product",
            ["Products.RemoveProductBtn"] = "Remove Product",

            ["Section.StructureSetup"] = "Structure Setup",

            ["Structure.Title"] = "Structure Setup",
            ["Structure.Subtitle"] = "Define your products, environments, target groups, and targets.",
            ["Structure.Environments"] = "Environments",
            ["Structure.AddEnvironment"] = "Add Environment",
            ["Structure.TargetGroups"] = "Target Groups",
            ["Structure.AddTargetGroup"] = "Add Target Group",
            ["Structure.Targets"] = "Targets",
            ["Structure.AddTarget"] = "Add Target",

            ["Phase.Hub"] = "1. Products & Environments",
            ["Phase.Configuration"] = "2. Configuration",
            ["Phase.Overview"] = "3. Overview",

            ["Mode.ExpertMode"] = "Expert Mode",
            ["Mode.EasyModeHint"] = "Starting in Easy Mode — showing only essential settings. You can switch to Expert Mode at any time for full control over all configuration options.",

            ["Hub.Title"] = "Products and Environments",
            ["Hub.Subtitle"] = "Manage your products and environments. Configure each combination individually.",
            ["Hub.Products"] = "Products",
            ["Hub.Environments"] = "Environments",
            ["Hub.AddProduct"] = "Add Product",
            ["Hub.AddEnvironment"] = "Add Environment",
            ["Hub.ProductAlias"] = "Product Alias",
            ["Hub.EnvironmentName"] = "Environment Name",
            ["Hub.CombinationTable"] = "Select a configuration to edit",
            ["Hub.WizardStatus"] = "Wizard",
            ["Hub.ValidationStatus"] = "Validation",
            ["Hub.StartDetailedConfig"] = "Start Detailed Configuration",
            ["Hub.StartOver"] = "Start Over",
            ["Hub.StartOverTitle"] = "Start Over?",
            ["Hub.StartOverMessage"] = "This will discard all your current settings, including products, environments, and all wizard configurations. This action cannot be undone.",
            ["Hub.StartOverConfirm"] = "Yes, Start Over",
            ["Hub.GoToOverview"] = "Go to Overview",
            ["Hub.Todo"] = "TODO",
            ["Hub.Done"] = "DONE",
            ["Hub.Valid"] = "Valid",
            ["Hub.Invalid"] = "Invalid",
            ["Hub.NoProductSelected"] = "Select a product to see its environments.",
            ["Hub.NoCombinations"] = "No product/environment combinations defined yet.",
            ["Hub.ContextHeader"] = "Product: {0} | Environment: {1}",
            ["Hub.CompleteAndReturn"] = "Complete and Return to Hub",
            ["Hub.BackToHub"] = "Back to Hub",
            ["Hub.GuideAddProduct"] = "Start by adding your first product on the left.",
            ["Hub.GuideSelectProduct"] = "Select a product to add environments for it.",
            ["Hub.GuideAddEnv"] = "Now add at least one environment for this product.",
            ["Hub.GuideSelectCombo"] = "Add products and environments. Then select a combination from the table below and click Start Detailed Configuration."
        },

        ["de"] = new Dictionary<string, string>
        {
            ["Welcome.Title"] = "Willkommen zum RayMigrator Konfigurations-Assistenten",
            ["Welcome.Subtitle"] = "Erstellen oder importieren Sie Ihre RayMigrator-Konfiguration",
            ["Welcome.CreateNew"] = "Neue Konfiguration erstellen",
            ["Welcome.CreateNewDescription"] = "Starten Sie von Grund auf mit dem geführten Einrichtungsassistenten.",
            ["Welcome.UploadExisting"] = "Bestehende Konfiguration bearbeiten",
            ["Welcome.UploadDescription"] = "Öffnen Sie bestehende appsettings*.json Dateien von Ihrem Rechner, um sie zu prüfen und zu bearbeiten.",
            ["Welcome.SelectFiles"] = "Dateien auswählen",
            ["Welcome.UploadPrivacy"] = "Ihre Dateien verbleiben auf Ihrem Rechner und werden nur lokal im Browser verarbeitet.",
            ["Welcome.CreatePrivacy"] = "Ihre Daten verbleiben auf Ihrem Rechner und werden nur lokal im Browser verarbeitet.",
            ["Welcome.GoToOverview"] = "Zur Übersicht",
            ["Welcome.WalkThrough"] = "Assistent durchlaufen",
            ["Welcome.Language"] = "Sprache",

            ["Repository.Title"] = "Repository-Datenbank",
            ["Repository.Subtitle"] = "Wählen Sie die Datenbank-Engine für das Migrations-Repository.",
            ["Repository.TableBaseName"] = "Tabellen-Basisname",

            ["Products.Title"] = "Produkte",
            ["Products.Subtitle"] = "Definieren Sie die Produkte, die RayMigrator verwalten soll.",
            ["Products.AddProduct"] = "Produkt hinzufügen",
            ["Products.ProductAlias"] = "Produkt-Alias",
            ["Products.RemoveProduct"] = "Entfernen",
            ["Products.Empty"] = "Keine Produkte definiert. Fügen Sie mindestens ein Produkt hinzu.",
            ["Products.MigrationFilesRootDirectory"] = "Migrations-Stammverzeichnis",

            ["ProductDetail.Title"] = "Produktdetails",
            ["ProductDetail.Environments"] = "Umgebungen",
            ["ProductDetail.AddEnvironment"] = "Umgebung hinzufügen",
            ["ProductDetail.TargetGroups"] = "TargetGroups",
            ["ProductDetail.AddTargetGroup"] = "TargetGroup hinzufügen",
            ["ProductDetail.TargetGroupAlias"] = "TargetGroup-Alias",
            ["ProductDetail.DatabaseType"] = "Datenbanktyp",
            ["ProductDetail.Targets"] = "Targets",
            ["ProductDetail.AddTarget"] = "Target hinzufügen",
            ["ProductDetail.TargetAlias"] = "Target-Alias",
            ["ProductDetail.RemoveTarget"] = "Entfernen",
            ["ProductDetail.RemoveTargetGroup"] = "Entfernen",
            ["ProductDetail.RemoveEnvironment"] = "Entfernen",

            ["OptionalFeatures.Title"] = "Optionale Features",
            ["OptionalFeatures.Subtitle"] = "Konfigurieren Sie optionale Features für Ihr Setup.",
            ["OptionalFeatures.DatabaseLogging"] = "Datenbank-Logging",
            ["OptionalFeatures.DatabaseLoggingHint"] = "Dringend empfohlen. Deaktivierung bedeutet keine Migrations-Logs in der Datenbank.",
            ["OptionalFeatures.CliTools"] = "CLI Tools",
            ["OptionalFeatures.CliToolsHint"] = "Aktivieren Sie dies, wenn Migrationen über externe CLI-Tools ausgeführt werden sollen.",

            ["Summary.Title"] = "Setup-Zusammenfassung",
            ["Summary.Subtitle"] = "Überprüfen Sie Ihr Setup bevor Sie zur Detailkonfiguration fortfahren.",
            ["Summary.Confirm"] = "Bestätigen und fortfahren",
            ["Summary.Edit"] = "Setup bearbeiten",
            ["Summary.Restart"] = "Setup neu starten",
            ["Summary.Repository"] = "Repository",
            ["Summary.DatabaseLogging"] = "Datenbank-Logging",
            ["Summary.CliTools"] = "CLI Tools",
            ["Summary.Enabled"] = "Aktiviert",
            ["Summary.Disabled"] = "Deaktiviert",

            ["Phase2.Title"] = "Detailkonfiguration",
            ["Phase2.Subtitle"] = "Konfigurieren Sie jeden Bereich im Detail.",
            ["Phase2.BackToStart"] = "Zurück zum Start",
            ["Phase2.Completed"] = "Alle Bereiche wurden konfiguriert.",
            ["Phase2.GoToOverview"] = "Weiter zur Übersicht",

            ["Phase3.Title"] = "Konfigurations-Übersicht",
            ["Phase3.Subtitle"] = "Prüfen und bearbeiten Sie alle Bereiche. Laden Sie die Konfigurationsdateien herunter.",
            ["Phase3.Download"] = "ZIP herunterladen",

            ["Section.Repository"] = "Repository",
            ["Section.DatabaseLogging"] = "Datenbank-Logging",
            ["Section.CliTools"] = "CLI Tools",
            ["Section.ProductDefaults"] = "Produkt-Standards",
            ["Section.ProductSettings"] = "Produkt-Einstellungen",
            ["Section.Serilog"] = "Serilog",

            ["Common.Next"] = "Vor",
            ["Common.Back"] = "Zurück",
            ["Common.Save"] = "Speichern",
            ["Common.Cancel"] = "Abbrechen",
            ["Common.Add"] = "Hinzufügen",
            ["Common.Remove"] = "Entfernen",
            ["Common.Edit"] = "Bearbeiten",
            ["Common.Alias"] = "Alias",
            ["Common.ConnectionString"] = "Verbindungszeichenfolge",
            ["Common.DatabaseType"] = "Datenbanktyp",
            ["Common.SchemaName"] = "Schemaname",
            ["Common.Errors"] = "Fehler",
            ["Common.Warnings"] = "Warnungen",
            ["Common.Valid"] = "Gültig",
            ["Common.InheritedFrom"] = "Geerbt von",
            ["Common.Override"] = "Überschreiben",
            ["Common.Default"] = "Standard",
            ["Common.Timeout"] = "Befehls-Timeout (s)",
            ["Common.MaxRetries"] = "Max. Wiederholungen",
            ["Common.WaitTime"] = "Wartezeit (ms)",
            ["Common.Close"] = "Schließen",

            ["Env.Tabs"] = "Konfigurationsdateien",
            ["Env.Base"] = "Basis (appsettings.json)",

            ["Promotion.Applied"] = "Defaults erfolgreich hochgestuft",

            ["JsonPreview.Title"] = "JSON-Vorschau",
            ["Export.Success"] = "Konfiguration erfolgreich exportiert.",

            ["Matrix.Title"] = "Produkt- / Umgebungsmatrix",
            ["Matrix.Empty"] = "Noch keine Produkte oder Umgebungen definiert.",
            ["Matrix.Configured"] = "Konfiguriert",
            ["Matrix.BaseOnly"] = "Nur Basis",
            ["Matrix.Product"] = "Produkt",

            ["DbLogging.Disabled"] = "Datenbank-Logging ist deaktiviert. Aktivieren Sie es in der Setup-Phase.",
            ["DbLogging.Recommendation"] = "Datenbank-Protokollierung wird dringend empfohlen. Sie bietet eine vollständige Nachverfolgung aller Migrationsläufe, einschließlich welche Skripte ausgeführt wurden, deren Ergebnisse und aufgetretene Fehler. Ohne Datenbank-Protokollierung sind diese Informationen nur in der Konsolenausgabe oder in Datei-Logs verfügbar, die nach dem Sitzungsende verloren gehen können. In Produktionsumgebungen ist die Datenbank-Protokollierung unverzichtbar für die Fehlersuche bei fehlgeschlagenen Migrationen und die Verifizierung der Deployment-Historie.",
            ["DbLogging.TableBaseName"] = "Tabellen-Basisname",
            ["DbLogging.MinimumLevel"] = "Mindest-Level",
            ["DbLogging.Enable"] = "Datenbank-Logging aktivieren",

            ["CliTools.Empty"] = "Keine CLI-Tools konfiguriert. Fügen Sie Tools hinzu, um Migrationen über externe CLI-Tools auszuführen.",
            ["CliTools.ExecutablePath"] = "Ausführbarer Pfad",
            ["CliTools.ArgumentTemplate"] = "Argument-Vorlage",
            ["CliTools.InputMode"] = "Eingabemodus",
            ["CliTools.SuccessExitCodes"] = "Erfolgs-Exit-Codes",
            ["CliTools.SuccessExitCodesHelp"] = "Komma-getrennt. Beispiele: 0  oder  0, 1..5  oder  0, 10..",
            ["CliTools.AddTool"] = "CLI-Tool hinzufügen",

            ["CliParams.DialogTitle"] = "CLI-Tool-Parameter",
            ["CliParams.TemplateReference"] = "Argument-Vorlage",
            ["CliParams.Required"] = "Dieser Parameter ist erforderlich",
            ["CliParams.NoParametersFound"] = "Keine Parameter in der Argument-Vorlage gefunden.",
            ["CliParams.SetParameters"] = "Parameter festlegen",
            ["CliParams.EditParameters"] = "Parameter bearbeiten",
            ["CliParams.ClearParameters"] = "Parameter entfernen",
            ["CliParams.InheritedFrom"] = "Geerbt von",

            ["ProductDefaults.MigrationErrorAction"] = "Migrationsfehler-Aktion",
            ["ProductDefaults.RollbackErrorAction"] = "Rollback-Fehler-Aktion",
            ["ProductDefaults.MigrationFilesExtension"] = "Migrationsdatei-Erweiterung",
            ["ProductDefaults.RollbackPreExtension"] = "Rollback-Vorerweiterung",
            ["ProductDefaults.MigrationFilesEncoding"] = "Migrationsdatei-Kodierung",
            ["ProductDefaults.RequireRollbackFile"] = "Rollback-Datei erforderlich",
            ["ProductDefaults.StopRollbackOnMissingRollbackFile"] = "Rollback bei fehlender Rollback-Datei stoppen",
            ["ProductDefaults.TgStopRollbackOnMissingRollbackFile"] = "Rollback bei fehlender Rollback-Datei stoppen (TargetGroups-Standard)",
            ["ProductDefaults.TargetGroupDefaultsTitle"] = "TargetGroup-Standards",
            ["ProductDefaults.TargetMigrationOrder"] = "Target Migrationsreihenfolge",
            ["ProductDefaults.HashValidationScope"] = "Hash-Validierungsbereich",
            ["ProductDefaults.TargetDefaultsTitle"] = "Target-Standards",
            ["ProductDefaults.DefaultCliToolAlias"] = "Verwende CLI-Tool",

            ["Products.TargetGroupMigrationOrder"] = "TargetGroup-Migrationsreihenfolge",
            ["Products.TargetGroupMigrationOrderHint"] = "Kommagetrennte Liste von TargetGroup-Aliasen (z.B. Backend,Frontend)",

            ["Serilog.SinkName"] = "Sink-Name",
            ["Serilog.LevelOverrides"] = "Level-Überschreibungen",
            ["Serilog.MinimumLevel"] = "Mindest-Level",
            ["Serilog.AddSink"] = "Sink hinzufügen",

            ["Upload.FilesLoaded"] = "Datei(en) geladen",

            ["Help.ValidValues"] = "Gültige Werte",
            ["Help.Default"] = "Standardwert",
            ["Help.Examples"] = "Beispiele",
            ["Help.JsonConfigPath"] = "JSON-Konfigurationspfad",
            ["Help.InheritedBy"] = "Vererbt an",

            ["Products.OverrideProductDefaults"] = "Produkt-Standards überschreiben",
            ["Products.OverrideTargetGroupDefaults"] = "TargetGroup-Standards überschreiben",
            ["Products.OverrideTargetDefaults"] = "Target-Standards überschreiben",
            ["Products.AddTargetBtn"] = "Target hinzufügen",
            ["Products.AddTargetGroupBtn"] = "Target Group hinzufügen",
            ["Products.AddProductBtn"] = "Produkt hinzufügen",
            ["Products.RemoveProductBtn"] = "Produkt entfernen",

            ["Section.StructureSetup"] = "Strukturaufbau",

            ["Structure.Title"] = "Strukturaufbau",
            ["Structure.Subtitle"] = "Definieren Sie Ihre Produkte, Umgebungen, TargetGroups und Targets.",
            ["Structure.Environments"] = "Umgebungen",
            ["Structure.AddEnvironment"] = "Umgebung hinzufügen",
            ["Structure.TargetGroups"] = "TargetGroups",
            ["Structure.AddTargetGroup"] = "TargetGroup hinzufügen",
            ["Structure.Targets"] = "Targets",
            ["Structure.AddTarget"] = "Target hinzufügen",

            ["Phase.Hub"] = "1. Produkte & Umgebungen",
            ["Phase.Configuration"] = "2. Konfiguration",
            ["Phase.Overview"] = "3. Übersicht",

            ["Mode.ExpertMode"] = "Experten-Modus",
            ["Mode.EasyModeHint"] = "Start im Easy-Modus — es werden nur die wesentlichen Einstellungen angezeigt. Sie können jederzeit in den Experten-Modus wechseln für volle Kontrolle über alle Konfigurationsoptionen.",

            ["Hub.Title"] = "Produkte und Umgebungen",
            ["Hub.Subtitle"] = "Verwalten Sie Ihre Produkte und Umgebungen. Konfigurieren Sie jede Kombination einzeln.",
            ["Hub.Products"] = "Produkte",
            ["Hub.Environments"] = "Umgebungen",
            ["Hub.AddProduct"] = "Produkt hinzufügen",
            ["Hub.AddEnvironment"] = "Umgebung hinzufügen",
            ["Hub.ProductAlias"] = "Produkt-Alias",
            ["Hub.EnvironmentName"] = "Umgebungsname",
            ["Hub.CombinationTable"] = "Konfiguration zum Bearbeiten auswählen",
            ["Hub.EditProduct"] = "Rename Product",
            ["Hub.EditEnvironment"] = "Rename Environment",
            ["Hub.RenamePrompt"] = "Enter new name:",
            ["Hub.WizardStatus"] = "Assistent",
            ["Hub.ValidationStatus"] = "Validierung",
            ["Hub.StartDetailedConfig"] = "Detailkonfiguration starten",
            ["Hub.StartOver"] = "Neu beginnen",
            ["Hub.StartOverTitle"] = "Neu beginnen?",
            ["Hub.StartOverMessage"] = "Alle bisherigen Einstellungen gehen verloren, einschließlich Produkte, Umgebungen und aller Wizard-Konfigurationen. Diese Aktion kann nicht rückgängig gemacht werden.",
            ["Hub.StartOverConfirm"] = "Ja, neu beginnen",
            ["Hub.GoToOverview"] = "Zur Übersicht",
            ["Hub.Todo"] = "TODO",
            ["Hub.Done"] = "DONE",
            ["Hub.Valid"] = "Gültig",
            ["Hub.Invalid"] = "Ungültig",
            ["Hub.NoProductSelected"] = "Wählen Sie ein Produkt, um dessen Umgebungen anzuzeigen.",
            ["Hub.NoCombinations"] = "Noch keine Produkt-/Umgebungskombinationen definiert.",
            ["Hub.ContextHeader"] = "Produkt: {0} | Umgebung: {1}",
            ["Hub.CompleteAndReturn"] = "Abschließen und zum Hub zurückkehren",
            ["Hub.BackToHub"] = "Zurück zum Hub",
            ["Hub.EditProduct"] = "Produkt umbenennen",
            ["Hub.EditEnvironment"] = "Umgebung umbenennen",
            ["Hub.RenamePrompt"] = "Neuen Namen eingeben:",
            ["Hub.GuideAddProduct"] = "Beginnen Sie, indem Sie links Ihr erstes Produkt anlegen.",
            ["Hub.GuideSelectProduct"] = "Wählen Sie ein Produkt aus, um Umgebungen dafür anzulegen.",
            ["Hub.GuideAddEnv"] = "Fügen Sie nun mindestens eine Umgebung für dieses Produkt hinzu.",
            ["Hub.GuideSelectCombo"] = "Legen Sie Produkte und Umgebungen an. Wählen Sie dann eine Kombination aus der Tabelle und klicken Sie auf Detailkonfiguration starten."
        }
    };
}
