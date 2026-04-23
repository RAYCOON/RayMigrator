# Bootstrap Options

Bootstrap options define the minimal settings needed before the full DI container is built. These include Serilog logging configuration and the optional Admin-DB connection used by RayMigrator Studio.

## Configuration Loading

All configuration files (up to 4 levels of the JSON hierarchy) are loaded and merged in a single pass. Serilog is then initialized from the merged `RayMigrator` section. The full `RayMigratorOptions` are subsequently bound and validated when the DI host is built.

## RayMigratorBootstrapOptions Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `AdminDb` | `AdminDbOptions` | No | `null` | Admin database connection settings. When set (by RayMigrator Studio), Products/Environments/Targets are loaded from the Admin-DB instead of JSON files. |
| `Serilog` | `SerilogOptions` | No | `null` | Marker for Serilog configuration (read by Serilog's own configuration reader) |

## AdminDbOptions Properties

`AdminDbOptions` is consumed by RayMigrator Studio. It is not used by the standalone CLI.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Provider` | string | Yes | `""` | Database provider for the Admin-DB. Supported values: `Sqlite`, `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql` |
| `ConnectionString` | string | Yes | `""` | Connection string for the Admin-DB. Supports `{ENV:VARIABLE_NAME}` placeholder syntax. |
| `SchemaName` | string | No | `null` | Optional schema name for non-SQLite providers (e.g., `admin` for PostgreSQL/SqlServer). |

## Example Configuration

### Minimal Bootstrap (Serilog only)

When no bootstrap options are set beyond Serilog, RayMigrator reads all configuration from `appsettings.json` files.

```json
{
  "RayMigrator": {
    "Serilog": {
      "MinimumLevel": { "Default": "Information" },
      "WriteTo": [{ "Name": "Console" }]
    }
  }
}
```

## Bootstrap vs Full Configuration

Bootstrap options are **not** part of the settings inheritance chain (`ProductDefaults` / `TargetGroupDefaults` / `TargetDefaults`). They are read from the merged configuration section before the DI host is built.

| Scope | Bootstrap Options | Full Options |
|-------|-------------------|--------------|
| Loaded when | Before DI host construction (Serilog init) | During DI host construction |
| Source | Merged `RayMigrator` section (all 4 JSON files) | Same merged section — bound and validated via `IOptions<RayMigratorOptions>` |
| Inheritance | None | Full Defaults inheritance chain |
| Section | `RayMigrator.Serilog`, `RayMigrator.AdminDb` | `RayMigrator.Repository`, `RayMigrator.Products`, etc. |

## Related Documentation

- [Execution Modes](../02-core-concepts/execution-modes.md) — Migration order, run modes
- [Configuration Hierarchy](appsettings-hierarchy.md) — File precedence for full configuration
- [Settings and Inheritance Overview](settings-inheritance-overview.md) — Complete settings map
- [CLI Reference](../08-cli-reference/command-reference.md) — CLI command reference
