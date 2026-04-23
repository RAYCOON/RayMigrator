# Directory Structure

Migration files are organized in a hierarchical directory structure. Products with exactly one target group may use either the traditional layout (with a target group subdirectory) or a flat layout (files directly under the release directory).

## Traditional Layout

```
{MigrationFilesRootDirectory}/
├── migsettings.txt                      # Product-wide settings
├── migsettings.{Environment}.txt        # Environment overrides
├── {ReleaseVersion}/
│   ├── migsettings.txt                  # Release-wide settings
│   ├── migsettings.{Environment}.txt    # Release environment overrides
│   ├── {TargetGroupAlias}/
│   │   ├── migsettings.txt              # Target group settings
│   │   ├── migsettings.{Environment}.txt
│   │   ├── ###_Description.sql          # Migration file
│   │   ├── ###_Description.rollback.sql # Rollback script
│   │   └── ###_Description.{Env}.sql    # Environment-specific
│   └── {AnotherTargetGroup}/
│       └── ...
└── {AnotherRelease}/
    └── ...
```

## Flat Layout (Single Target Group Only)

When a product has exactly one target group, migration files may be placed directly under the release directory, omitting the target group subdirectory:

```
{MigrationFilesRootDirectory}/
├── migsettings.txt                      # Product-wide settings
├── migsettings.{Environment}.txt        # Environment overrides
├── {ReleaseVersion}/
│   ├── migsettings.txt                  # Release-wide settings (applies to all files)
│   ├── migsettings.{Environment}.txt    # Release environment overrides
│   ├── ###_Description.sql              # Migration file (directly under release)
│   ├── ###_Description.rollback.sql     # Rollback script
│   └── ###_Description.{Env}.sql        # Environment-specific
└── {AnotherRelease}/
    └── ...
```

The target group alias is auto-assigned from the single configured target group. No target group subdirectory needs to exist on disk.

### Detection Rules

- **Per-release**: Each release directory is detected independently. If a subdirectory matching the target group alias exists, the traditional layout is used for that release. If no such subdirectory exists, the flat layout is used.
- **Mixed mode**: A product may have some releases using the flat layout and others using the traditional layout. This is fully supported.
- **Ambiguous state**: If a release contains migration files both directly in the release directory (flat) and inside the target group subdirectory (traditional), the run is aborted with a `ConfigurationValidationException` for all run modes.
- **Case sensitivity**: Directory names must exactly match the `TargetGroup.Alias` in configuration (case-sensitive). A directory whose name matches the alias only case-insensitively causes a `ConfigurationValidationException`. Rename the directory to match exactly.
- **Multi-target group products**: The flat layout is not supported when a product has more than one target group. Each release must contain subdirectories matching each target group alias.

## Example Structure

### Traditional Layout (Multiple Target Groups)

```
Testing/MigrationFiles/Tests_SqlServer/
├── migsettings.txt
├── migsettings.Development.txt
├── migsettings.Production.txt
├── Release 1.0/
│   ├── migsettings.txt
│   ├── Backend/
│   │   ├── 01_ooc_login.Docker.sql
│   │   ├── 01_ooc_login.Production.sql
│   │   ├── 10_CreateDataModel.sql
│   │   ├── 20_InsertMasterData.sql
│   │   └── 20_InsertMasterData.rollback.sql
│   └── Frontend/
│       └── 00_CreateDataModel.sql
├── Release 1.1/
│   ├── migsettings.txt
│   ├── Backend/
│   │   ├── migsettings.txt
│   │   ├── 01_InsertDynamicData.sql
│   │   └── 01_InsertDynamicData.rollback.sql
│   └── Frontend/
│       ├── 01_InsertDynamicData.sql
│       └── 01_InsertDynamicData.rollback.sql
├── Release 1.2/
│   ├── migsettings.txt
│   ├── Backend/
│   │   ├── 00_AddSexOther.sql
│   │   ├── 00_AddSexOther.rollback.sql
│   │   ├── 01_AddLoginPersonOther.sql
│   │   └── 01_AddLoginPersonOther.rollback.sql
│   └── Frontend/
│       ├── 01_AddUserProfileAndUserPreferences.sql
│       └── 01_AddUserProfileAndUserPreferences.rollback.sql
└── Release 1.3/
    ├── migsettings.txt
    ├── Backend/
    │   ├── 01_AddAlexLee2.sql
    │   └── 01_AddAlexLee2.rollback.sql
    └── Frontend/
        ├── migsettings.txt
        ├── 01_AddAlexLee2ProfileAndUserPreferences-Error.sql
        └── 01_AddAlexLee2ProfileAndUserPreferences-Error.rollback.sql
```

Test migration files for Docker-based engines are organized per engine (`Tests_SqlServer/`, `Tests_PostgreSQL/`, `Tests_MariaDb/`, `Tests_MySql/`), each with the same directory structure. Additional specialized test directories exist: `Tests_Success_*` (one per Docker-based engine: `Tests_Success_SqlServer`, `Tests_Success_PostgreSQL`, `Tests_Success_MariaDb`, `Tests_Success_MySql`) contains the same layout with all rollback files present for success-path testing, and `Tests_SqlCmdDemo` is used for CLI tool execution testing via an external tool. Engine integration tests use a separate migration files directory at `Raycoon.RayMigrator.Tests.Engine/MigrationFiles/`, which contains per-engine subdirectories (`SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite`) and corresponding frontend subdirectories (`SqlServer_Frontend`, `PostgreSQL_Frontend`, `MariaDb_Frontend`, `MySql_Frontend`, `Sqlite_Frontend`).

### Flat Layout (Single Target Group)

A product with a single target group `"Backend"` can use the flat layout:

```
Migrations/MyProduct/
├── migsettings.txt
├── Release 1.0/
│   ├── migsettings.txt
│   ├── 01_CreateTables.sql
│   ├── 01_CreateTables.rollback.sql
│   └── 02_InsertData.sql
├── Release 1.1/
│   ├── 01_AddColumn.sql
│   └── 01_AddColumn.rollback.sql
└── Release 2.0/           # Mixed mode: this release uses traditional layout
    └── Backend/
        ├── 01_Refactor.sql
        └── 01_Refactor.rollback.sql
```

The `Release 2.0` directory shows mixed-mode usage: earlier releases use flat layout while `Release 2.0` switched to the traditional layout with a `Backend/` subdirectory. Both are valid within the same product.

## Directory Levels

### Level 1: Product Root

`{MigrationFilesRootDirectory}/`

- Contains product-wide `migsettings.txt`
- Contains release version directories
- Configured via `Product.MigrationFilesRootDirectory`

### Level 2: Release Version

`{ReleaseVersion}/`

- Any naming convention (e.g., "Release 1.0", "v2.0", "2025-01")
- Contains target group directories (traditional layout) or migration files directly (flat layout)
- Optional `migsettings.txt` for release-wide settings

### Level 3: Target Group (Traditional Layout)

`{TargetGroupAlias}/`

- **Must match** `TargetGroup.Alias` in configuration (case-sensitive)
- Contains migration files
- Contains `migsettings.txt` for directory settings

### Level 3: Migration Files (Flat Layout)

In flat layout, migration files sit directly under the release directory at level 2. There is no level-3 subdirectory. A `migsettings.txt` at the release directory level applies to all migration files in that release.

## Matching Rules

### Target Group Matching

In the traditional layout, the directory name must exactly match `TargetGroup.Alias` (case-sensitive):

```json
{
  "TargetGroups": [{
    "Alias": "Backend"
  }]
}
```

Matches directory: `Release 1.0/Backend/`

In the flat layout (single target group only), no directory matching is required. The single configured target group alias is assigned automatically to all files found directly under a release directory.

### Release Version Sorting

Releases are sorted alphabetically by directory name:

```
Release 1.0/    → Executed first
Release 1.1/    → Executed second
Release 2.0/    → Executed third
```

**Recommendation**: Use consistent naming (e.g., `Release 1.0`, `Release 1.1`)

### File Sorting

Files are sorted by their relative path using ordinal case-insensitive comparison:

```
01_First.sql
02_Second.sql
10_Tenth.sql
```

## Environment-Specific Files

Files with environment suffix are only included for that environment. Generic files (no suffix) are always included:

| Environment | Files Discovered |
|-------------|-----------------|
| Development | `001_Create.sql` |
| Production | `001_Create.sql`, `001_Create.Production.sql` |
| Docker | `001_Create.sql`, `001_Create.Docker.sql` |

**Note**: There is no automatic precedence — if both generic and environment-specific files exist, both are discovered. To use environment-specific variants exclusively, do not create a generic version of the same file. See [Environment-Specific Files](environment-specific.md) for details.

## Configuration Example

### Multiple Target Groups (Traditional Layout Required)

```json
{
  "Products": [{
    "Alias": "MyProduct",
    "MigrationFilesRootDirectory": "Migrations/MyProduct",
    "TargetGroups": [
      { "Alias": "Backend", "DatabaseType": "SqlServer", ... },
      { "Alias": "Frontend", "DatabaseType": "SqlServer", ... }
    ]
  }]
}
```

Expected directories:
- `Migrations/MyProduct/*/Backend/`
- `Migrations/MyProduct/*/Frontend/`

### Single Target Group (Flat Layout Supported)

```json
{
  "Products": [{
    "Alias": "MyProduct",
    "MigrationFilesRootDirectory": "Migrations/MyProduct",
    "TargetGroups": [
      { "Alias": "Backend", "DatabaseType": "SqlServer", ... }
    ]
  }]
}
```

Accepted directory layouts per release:
- **Traditional**: `Migrations/MyProduct/*/Backend/` — files inside the `Backend/` subdirectory
- **Flat**: `Migrations/MyProduct/*/` — files directly under the release directory (no `Backend/` subdirectory)
- **Mixed across releases**: some releases use flat, others use traditional — both are valid within the same product

## Related Documentation

- [File Naming](file-naming.md) - Naming conventions
- [TOML Metadata](toml-metadata.md) - File metadata
- [migsettings Files](migsettings-files.md) - Control files
- [Product Options](../06-configuration-reference/product-options.md)
