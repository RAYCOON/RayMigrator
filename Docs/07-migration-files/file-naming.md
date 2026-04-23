# File Naming

Migration files follow specific naming conventions for proper ordering and identification.

**Important**: RayMigrator does **not** parse the filename to extract a sequence number or description. The filename is used only as a sort key to determine execution order. The human-readable description is defined in the [TOML metadata](toml-metadata.md) block inside the file, not derived from the filename.

## Migration Files

### Pattern

```
{OrderPrefix}_{Name}.{Extension}
```

| Component | Description | Example |
|-----------|-------------|---------|
| OrderPrefix | Numeric prefix for sort ordering | `001`, `01`, `00`, `10`, `20` |
| Name | Human-readable identifier | `CreateDataModel`, `InsertMasterData` |
| Extension | File extension (configurable) | `sql` |

The underscore between prefix and name is a convention, not a requirement. RayMigrator sorts files by their full relative path as a string, so the prefix controls execution order within a directory.

### Examples

From the test migration files:

```
00_CreateDataModel.sql
01_ooc_login.Docker.sql
10_CreateDataModel.sql
20_InsertMasterData.sql
01_InsertDynamicData.sql
01_AddLoginPersonOther.sql
01_AddAlexLee2ProfileAndUserPreferences-Error.sql
```

## Rollback Files

### Pattern

```
{BaseName}.{RollbackPreExtension}.{Extension}
```

Where `{BaseName}` is the filename without its final extension. Default `RollbackPreExtension`: `rollback`

The rollback filename is constructed by inserting the rollback pre-extension between the base name and the file extension. Internally, `Path.GetFileNameWithoutExtension()` strips the last extension, then `.{rollbackPreExtension}.{fileExtension}` is appended.

### Examples

```
20_InsertMasterData.rollback.sql        # Rollback for 20_InsertMasterData.sql
01_InsertDynamicData.rollback.sql       # Rollback for 01_InsertDynamicData.sql
00_AddSexOther.rollback.sql             # Rollback for 00_AddSexOther.sql (note: 00 not 001)
```

### Custom Rollback Pre-Extension

Configurable at both `ProductDefaults` and individual `Products` level:

```json
{
  "ProductDefaults": {
    "MigrationRollbackFilesPreExtension": "down"
  }
}
```

Or per product:

```json
{
  "Products": [{
    "MigrationRollbackFilesPreExtension": "down"
  }]
}
```

Result: `20_InsertMasterData.down.sql`

Only letters and underscores are allowed (validated by regex `^[a-zA-Z_]+$`).

## Environment-Specific Files

### Pattern

```
{BaseName}.{Environment}.{Extension}
```

A file is considered environment-specific if, after stripping the file extension, the remaining name contains a dot. The part after the last dot is treated as the environment name. Environment matching is case-insensitive.

### Examples

From the test migration files:

```
10_CreateDataModel.sql              # All environments (no dot after stripping .sql)
01_ooc_login.Docker.sql             # Docker only
01_ooc_login.Production.sql         # Production only
```

### Combined with Rollback

For an environment-specific file, the rollback filename is derived by stripping only the final `.sql` extension and appending `.{rollbackPreExtension}.{Extension}`:

```
01_ooc_login.Docker.rollback.sql    # Rollback for 01_ooc_login.Docker.sql
```

## Sorting and Execution Order

### How Sorting Works

Files are sorted by their **full relative path** from the migration root directory using **ordinal case-insensitive comparison** (`StringComparer.OrdinalIgnoreCase`). This means:

1. The release directory name sorts first (e.g., `Release 1.0/` before `Release 1.1/`)
2. Then the target group directory name (e.g., `Backend/` before `Frontend/`)
3. Then the filename within each target group directory

Each file that passes filtering (not a rollback file, not excluded by environment) is assigned a sequential `FileOrderId` starting at 1, based on its position in the sorted list.

### Numeric Prefixes

Recommended for predictable ordering within a directory:

```
00_CreateDataModel.sql   → sorts first
01_ooc_login.Docker.sql  → sorts second
10_CreateDataModel.sql   → sorts third
20_InsertMasterData.sql  → sorts fourth
```

**Important**: Use consistent prefix length (e.g., always 2 digits) within each target group directory.

### String Sort

Files are sorted as strings, not numbers:

```
1_A.sql      → Position 1
10_B.sql     → Position 2 (before "2")
2_C.sql      → Position 3
```

**Fix**: Use zero-padded prefixes: `01_A.sql`, `02_C.sql`, `10_B.sql`

## Best Practices

### 1. Use Zero-Padded Numbers

```
01_CreateUsers.sql      ✓
02_CreateOrders.sql     ✓
10_AddIndexes.sql       ✓
```

Not:
```
1_CreateUsers.sql       ✗
2_CreateOrders.sql      ✗
10_AddIndexes.sql       ✗ (sorts before 2)
```

### 2. Descriptive Names

```
01_CreateUsersTable.sql                  ✓
02_InsertDefaultRoles.sql                ✓
03_AddUserEmailIndex.sql                 ✓
```

Not:
```
01_Migration.sql                         ✗
02_Update.sql                            ✗
03_Fix.sql                               ✗
```

### 3. Consistent Naming

Same pattern throughout a project:
```
01_CreateUsers.sql           (PascalCase)
02_CreateOrders.sql
```

Or:
```
01_create_users.sql          (snake_case)
02_create_orders.sql
```

### 4. Version in Release Directory

Don't repeat version in filename (the release version is extracted from the directory path):

```
Release 1.0/
└── Backend/
    └── 01_CreateUsers.sql           ✓

Release 1.0/
└── Backend/
    └── R1.0_01_CreateUsers.sql      ✗ (redundant)
```

## File Extension

Default: `sql`

Configurable at both `ProductDefaults` and individual `Products` level. Product-level values override `ProductDefaults`:

```json
{
  "ProductDefaults": {
    "MigrationFilesExtension": "sql"
  }
}
```

Or per product:

```json
{
  "Products": [{
    "MigrationFilesExtension": "ddl"
  }]
}
```

Only letters and underscores are allowed (validated by regex `^[a-zA-Z_]+$`). Other examples: `ddl`, `dml`, `script`.

The file extension is used both for discovering migration files (`*.{extension}`) and for identifying rollback and environment-specific files.

## File Encoding

Default: `UTF-8`

Configurable at both `ProductDefaults` and individual `Products` level. Product-level values override `ProductDefaults`:

```json
{
  "ProductDefaults": {
    "MigrationFilesEncoding": "UTF-8"
  }
}
```

Or per product:

```json
{
  "Products": [{
    "MigrationFilesEncoding": "iso-8859-1"
  }]
}
```

The value must be a valid .NET encoding name (e.g., `UTF-8`, `ASCII`, `iso-8859-1`). Some encodings (e.g., `windows-1252`) require `System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` on .NET. If the encoding name is invalid, a `ConfigurationValidationException` is thrown at startup.

The encoding is used when reading migration file content from disk. Hash computation and TOML parsing operate on the decoded string content, so changing the encoding does not affect hash validation as long as the file content decodes to the same string.

## Skipped Files

During file discovery, the following files are automatically skipped:

- **Rollback files**: Any file ending with `.{rollbackPreExtension}.{extension}` (e.g., `*.rollback.sql`)
- **Environment-specific files for other environments**: Files with an environment suffix that does not match the current environment
- **migsettings files**: Any file whose name starts with `migsettings` (case-insensitive). Note that migsettings files normally use the `.txt` extension and would not match the migration file glob (`*.sql` by default). This skip rule is a defensive guard that applies if a file like `migsettings_backup.sql` is present in the migration directory.

## Case Sensitivity

| OS | Case Sensitive |
|----|----------------|
| Windows | No |
| Linux | Yes |
| macOS | Usually No |

**Recommendation**: Use consistent casing (e.g., PascalCase for names).

Note that RayMigrator itself uses case-insensitive comparisons internally (`OrdinalIgnoreCase`) for sorting, rollback file detection, and environment matching. However, the filesystem may be case-sensitive (Linux), so the file must exist with the exact casing on disk.

## Related Documentation

- [Directory Structure](directory-structure.md)
- [TOML Metadata](toml-metadata.md)
- [Rollback Files](rollback-files.md)
- [Environment-Specific](environment-specific.md)
