using System.Text.Json.Nodes;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// In-memory mutable configuration model that mirrors the RayMigrator options hierarchy.
/// Cleaned version without TUI-specific properties (undo/redo, events, scoped editing).
/// </summary>
public class ConfigurationModel
{
    public RepositoryModel Repository { get; set; } = new();
    public DatabaseLoggingModel? DatabaseLogging { get; set; }
    public ProductDefaultsModel ProductDefaults { get; set; } = new();
    public List<ProductModel> Products { get; set; } = new();
    public SerilogModel Serilog { get; set; } = new();

    /// <summary>CLI tool configurations. Corresponds to Core's CliToolOptions[].</summary>
    public List<CliToolModel> CliTools { get; set; } = new();

    /// <summary>Change tracking -- set to true when any property is modified.</summary>
    public bool IsModified { get; set; }

    /// <summary>Logical file path (not used for IO in Core).</summary>
    public string? FilePath { get; set; }

    /// <summary>Role of this file in the appsettings hierarchy.</summary>
    public ConfigFileRole? FileRole { get; set; }

    /// <summary>
    /// Preserves the original JSON document so that unknown keys (e.g. AdminDb, ApiUrl)
    /// are not lost during round-trip load -> edit -> save.
    /// </summary>
    public JsonNode? PreservedDocument { get; set; }
}
