# Config-Wizard: Configuration File Hierarchy

The Config-Wizard manages the same 4-level `appsettings` file hierarchy used by RayMigrator at runtime.

## The 4-Level Hierarchy

```
Priority (lowest → highest)

1. appsettings.json                        Base — shared defaults
2. appsettings.{Environment}.json          Environment overrides (e.g. appsettings.Docker.json)
3. appsettings.{Product}.json              Product-specific config (e.g. appsettings.BookStore.json)
4. appsettings.{Product}.{Environment}.json  Most specific (e.g. appsettings.BookStore.Docker.json)
```

### Merge Semantics

Files are merged from lowest to highest priority:

- **JSON objects** are recursively merged — a higher-priority file's value wins for conflicting keys
- **Alias-keyed arrays** (`Products`, `TargetGroups`, `Targets`, `CliTools` — any array whose every element is a JSON object with a string `Alias` property) are merged by matching `Alias`: matching items are recursively merged, override items without a base match are appended, base items without an override match are preserved
- **Other JSON arrays** (e.g. `Serilog.WriteTo`) are completely replaced by the highest-priority file that contains them
- **Scalar values** are replaced by the higher-priority file's value

### ConfigFileRole Enum

| Value | Numeric | File Pattern |
|-------|---------|--------------|
| `Base` | 1 | `appsettings.json` |
| `Environment` | 2 | `appsettings.{Environment}.json` |
| `Product` | 3 | `appsettings.{Product}.json` |
| `ProductEnvironment` | 4 | `appsettings.{Product}.{Environment}.json` |

## File Family Discovery

When importing existing files, `ConfigurationFileParser.ClassifyFileName` parses each filename to classify its role.

**Classification logic**: A filename like `appsettings.json` is `Base` (no segments). A single-segment file like `appsettings.Docker.json` defaults to `Environment` (most common case). A two-segment file like `appsettings.BookStore.Docker.json` is `ProductEnvironment` with `Product = BookStore` and `Environment = Docker`. Files with three or more segments join all but the last segment as the product name and use the last segment as the environment.

## Working with Files in the Wizard

### Import

On the Welcome page the user can upload existing `appsettings*.json` files. `ConfigurationFileParser.Parse` classifies each file by name and populates the in-memory `WizardState` (base model, environment models, product models, and product-environment models). Unknown filenames are skipped; parse errors on individual files are silently ignored.

### Overview File Tabs

The Overview page displays one tab per file that would be written on export. The JSON for each file is produced by `ZipExportService.ComputeExportJsons`, which applies full hierarchy pruning: values that are overridden by every child combination are removed from the parent file so that each file stays minimal.

### Adding New Environments

The Hub page lets the user add environments to products. When a new environment is added, `WizardStateService.AddEnvironment` creates an in-memory environment model (a skeleton with a placeholder connection string) and scaffolds a product-environment model via `ConfigurationScaffolder.ScaffoldCombination`. On export, these are serialized as separate files.

### Export (ZIP Download)

The Overview page's Export button calls `ZipExportService.ExportAsync`, which builds an in-memory ZIP containing all hierarchy files plus an `example.env` file listing all `{ENV:}` variable names found across all exported configuration files.

### Round-Trip Safety

The wizard preserves unknown JSON keys. If your `appsettings.json` contains `AdminDb`, `ApiUrl`, or any other section not recognized by the wizard, those keys are preserved when the file is saved. The Web wizard manages 6 sections: `Repository`, `DatabaseLogging`, `ProductDefaults`, `Products`, `Serilog`, and `CliTools`.

## Scoped Editing

The Hub (Phase 2) shows a matrix of Products × Environments. Clicking a cell opens the Detailed Configuration scoped to that specific Product+Environment combination.

In scoped mode:
- The 6-step stepper operates on the `ConfigurationModel` stored in `WizardState.ProductEnvironmentModels["{Product}.{Env}"]`
- The model contains only the matching product
- On export, this model is serialized as `appsettings.{Product}.{Env}.json` with hierarchy pruning applied

## Expected Patterns in Practice

### Typical multi-environment setup

```
config/
├── appsettings.json              Base: Repository, Serilog, ProductDefaults
├── appsettings.Docker.json       Docker-specific connection strings
├── appsettings.Production.json   Production-specific connection strings
├── appsettings.BookStore.json    BookStore product definition
└── appsettings.BookStore.Docker.json  BookStore + Docker override
```

In this layout:
- `appsettings.json` + `appsettings.Docker.json` + `appsettings.BookStore.json` + `appsettings.BookStore.Docker.json` form the merge chain for scope `(BookStore, Docker)` at runtime
- When imported via the wizard, `appsettings.BookStore.Docker.json` is classified as `ProductEnvironment` (Product=BookStore, Environment=Docker); `appsettings.Docker.json` and `appsettings.BookStore.json` are each classified as `Environment` (single-segment files default to Environment role)

### Single-file setup

All configuration in one file is also valid:

```
config/
└── appsettings.json    All products, all connection strings (using {ENV:} placeholders)
```

## Related Documentation

- [Configuration Reference: appsettings hierarchy](../06-configuration-reference/appsettings-hierarchy.md)
- [Services Reference: ConfigFileMerger](./services.md#configfilemerger)
- [Overview](./overview.md)
