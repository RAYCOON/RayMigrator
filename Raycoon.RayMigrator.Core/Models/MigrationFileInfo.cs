// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Models;

/// <summary>
/// Represents a parsed migration file ready for execution.
/// Contains the file metadata, TOML configuration, SQL blocks, and computed hashes.
/// </summary>
public class MigrationFileInfo
{
    /// <summary>
    /// The filename without path (e.g., "10_CreateDataModel.sql").
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// The filename with relative path from the migration root directory
    /// (e.g., "Release 1.0/Backend/10_CreateDataModel.sql").
    /// </summary>
    public string FilenameWithRelativePath { get; set; } = string.Empty;

    /// <summary>
    /// The full absolute path to the file on disk.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Release version extracted from the folder path (e.g., "Release 1.0").
    /// </summary>
    public string ReleaseVersion { get; set; } = string.Empty;

    /// <summary>
    /// Target group alias extracted from the folder path (e.g., "Backend").
    /// </summary>
    public string TargetGroupAlias { get; set; } = string.Empty;

    /// <summary>
    /// Execution order derived from the file's position in the sorted file list.
    /// </summary>
    public int FileOrderId { get; set; }

    /// <summary>
    /// SHA256 hash of the entire file content.
    /// </summary>
    public string FileUpHash { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the TOML configuration section.
    /// </summary>
    public string? FileUpConfigHash { get; set; }

    /// <summary>
    /// SHA256 hash of the SQL content (all blocks concatenated, excluding TOML).
    /// </summary>
    public string FileUpBlocksHash { get; set; } = string.Empty;

    /// <summary>
    /// The individual SQL blocks extracted from the file (split by GO delimiter).
    /// </summary>
    public List<string> SqlBlocks { get; set; } = new();

    /// <summary>
    /// Total number of SQL blocks.
    /// </summary>
    public int FileUpBlocksTotal => SqlBlocks.Count;

    /// <summary>
    /// The raw TOML configuration string from the file header.
    /// </summary>
    public string? TomlConfigRaw { get; set; }

    /// <summary>
    /// JSON representation of the parsed TOML configuration.
    /// </summary>
    public string? FileUpConfigJson { get; set; }

    /// <summary>
    /// Whether a corresponding rollback file exists on disk.
    /// </summary>
    public bool MigrateDownFileExists { get; set; }

    /// <summary>
    /// Full path to the rollback file, if it exists.
    /// </summary>
    public string? RollbackFilePath { get; set; }

    // --- TOML Configuration Properties ---

    /// <summary>
    /// Whether to wrap the migration in a database transaction.
    /// Parsed from TOML "UseTransaction" property. Default: true.
    /// </summary>
    public bool UseTransaction { get; set; } = true;

    /// <summary>
    /// Whether UseTransaction was explicitly configured (via TOML header or migsettings).
    /// When false, UseTransaction holds the default value and was not actively set by the user.
    /// </summary>
    public bool UseTransactionExplicitlySet { get; set; }

    /// <summary>
    /// Human-readable description from TOML metadata.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Environment filter from TOML metadata. Null or empty means all environments.
    /// </summary>
    public List<string>? Environments { get; set; }

    /// <summary>
    /// Target filter from TOML metadata. Null or ["*"] means all targets.
    /// </summary>
    public List<string>? Targets { get; set; }

    /// <summary>
    /// Whether this migration should run every time, even if already applied.
    /// Parsed from TOML "RunAlways" property. Default: false.
    /// </summary>
    public bool RunAlways { get; set; }

    /// <summary>
    /// Whether this migration requires a rollback file.
    /// Resolved from configuration hierarchy: ProductDefaults → Product → migsettings → TOML.
    /// </summary>
    public bool RequireRollbackFile { get; set; } = true;

    /// <summary>
    /// Per-file override for MigrationErrorAction.
    /// Resolved from configuration hierarchy: ProductDefaults → Product → migsettings → TOML.
    /// Null means no override — the Product-level default is used.
    /// </summary>
    public MigrationErrorAction? MigrationErrorActionOverride { get; set; }

    /// <summary>
    /// Per-file override for RollbackErrorAction.
    /// Resolved from configuration hierarchy: ProductDefaults → Product → migsettings → TOML.
    /// Null means no override — the Product-level default is used.
    /// </summary>
    public RollbackErrorAction? RollbackErrorActionOverride { get; set; }

    /// <summary>
    /// CLI tool alias override for this file.
    /// Resolved from configuration hierarchy: ProductDefaults → Product → TargetGroup → Target → migsettings → TOML.
    /// Null means no override — the Target-level UseCliToolAlias (if any) is used, or the built-in DAL.
    /// </summary>
    public string? UseCliToolAlias { get; set; }
}
