# Configuration System

RayMigrator uses the .NET Options Pattern with a hierarchical configuration system. Currently, all configuration is loaded from JSON config files (Standalone mode). The `OperatingMode` enum defines additional modes (ManagedLocal, ManagedRemote) for use by RayMigrator Studio. See [Execution Modes](execution-modes.md#operating-mode) for the full operating mode reference.

## Operating Modes

The `OperatingMode` enum (`Raycoon.RayMigrator.Core/Configuration/Enums/OperatingMode.cs`) defines three modes:

1. **Standalone** (default, currently active in Engine) -- All configuration (Products, Targets, Repository, etc.) is loaded from `appsettings.json` files. `Program.Main` creates a `JsonOptionsSource` and calls `RunDirectMode()`.
2. **ManagedLocal** (implemented in RayMigrator Studio) -- Configuration loaded from a local Admin-DB. The `AdminDbOptions` class and `RayMigratorBootstrapOptions` are part of Engine's Core NuGet package; the actual Admin-DB services and schema live in Studio.
3. **ManagedRemote** (implemented in RayMigrator Studio) -- CLI operates as a thin client sending HTTP requests to a remote API server.

## Configuration File Hierarchy

Configuration files are loaded and merged in priority order (later overrides earlier). For complete details on file loading order, merge behavior, and environment detection, see [Configuration Hierarchy](../06-configuration-reference/appsettings-hierarchy.md).

> **Note**: Command-line arguments (`--product`, `--environment`, `--run-mode`, etc.) are NOT part of the configuration merge chain. They are parsed separately into `RayMigratorConsoleOptions` by System.CommandLine.

## Options Classes

### Console Options

`RayMigratorConsoleOptions` holds CLI arguments parsed by System.CommandLine. It is not bound from JSON configuration. Key properties:

- `Command` (`MigrationCommand`): The command to execute (None=0, MigrateUp=1, MigrateDown=2, ValidateHash=3, UpdateHash=4, Info=5, Baseline=6, FixIssues=7)
- `Product` (`string`): Product alias from `--product` / `-p`
- `Environment` (`string`): Target environment from `--environment` / `-env`
- `RunMode` (`MigrationRunMode`): Validate, Simulate, or Migrate via `--run-mode` / `-rm`
- `TargetReleaseVersion` (`string?`): For Migrate-Down and Baseline `--to-release` / `-tr`
- `TargetGroupAliases` (`string[]?`): Filter execution to specific target groups via `--target-group` / `-tg`
- `TargetGroupMigrationOrder` (`string[]?`): Explicit TargetGroup execution order via `--TargetGroup-MigrationOrder` / `-tgmo` (all aliases must be listed). Applies to Migrate-Up and Baseline only.
- `HashValidationScope` (`HashValidationScope?`): For Validate-Hash `--scope` / `-s`
- `ShowStartupInfo` (`bool`): Show startup information via `--startup-info` / `-si` (default: true)
- `RevealSensitiveData` (`bool`): Include sensitive data in logs via `--reveal-sensitive-data` / `-rsd` (default: false)
- `AllowOutOfOrder` (`bool?`): Allow out-of-order migration execution via `--allow-out-of-order` / `-ooo`
- `FixIssues` (`FixIssues?`): Fix command scope (OrphanedRuns, All) via `--scope` / `-s` (default: OrphanedRuns)
- `FixOlderThanMinutes` (`int?`): Fix command age threshold via `--older-than` / `-ot` (default: 60)
- `FixDryRun` (`bool?`): Fix command dry-run mode via `--dry-run`
- `FixAssumedMigrationStatus` (`MigrationStatus?`): Fix command status for orphaned migrations via `--last-migration-status` / `-lms`
- `StopRollbackOnMissingRollbackFile` (`bool?`): CLI override for the `StopRollbackOnMissingRollbackFile` configuration option via `--stop-rollback-on-missing-rollback-file` / `-sromrf` (default: null — defers to configuration)
- `ConfigDir` (`string?`): Override directory where RayMigrator searches for configuration files (`appsettings.json` hierarchy) via `--config-dir` / `-cd` (default: current working directory; always resolved to an absolute path at parse time)

Namespace: `Raycoon.RayMigrator.Core.Configuration.Options`

### Migration Options Hierarchy

`RayMigratorOptions` is the main configuration class bound from the `"RayMigrator"` section of the JSON configuration.

```
RayMigratorOptions
├── Repository (RepositoryOptions)
│   ├── DatabaseType                    (string, required)
│   ├── ConnectionString                (string?)
│   ├── SchemaName                      (string?)
│   ├── TableBaseName                   (string?)
│   ├── DbCommandTimeoutInSeconds       (int?, default: 60)
│   ├── DbCommandMaxRetries             (int?, default: 100)
│   └── DbCommandWaitTimeInMsBeforeRetry (int?, default: 250)
├── DatabaseLogging (DatabaseLoggingOptions)
│   ├── DatabaseType                    (string?)
│   ├── MinimumLevel                    (string?, LogLevel enum)
│   ├── ConnectionString                (string?)
│   ├── SchemaName                      (string?)
│   ├── TableBaseName                   (string?)
│   └── DbCommandTimeoutInSeconds       (int?, default: 20)
├── Serilog (SerilogOptions)
├── ProductDefaults (ProductDefaultOptions)
│   ├── MigrationErrorAction            (string?, MigrationErrorAction enum)
│   ├── RollbackErrorAction             (string?, RollbackErrorAction enum)
│   ├── MigrationFilesExtension         (string?, regex: ^[a-zA-Z_]+$)
│   ├── MigrationRollbackFilesPreExtension (string?, regex: ^[a-zA-Z_]+$)
│   ├── MigrationFilesEncoding          (string?, validated via Encoding.GetEncoding)
│   ├── RequireRollbackFile             (bool?)
│   ├── StopRollbackOnMissingRollbackFile (bool?)
│   ├── UseCliToolAlias                     (string?)
│   └── TargetGroupDefaults (TargetGroupDefaultOptions)
│       ├── TargetMigrationOrder              (string?, TargetMigrationOrder enum)
│       ├── HashValidationScope         (string?, HashValidationScope enum)
│       ├── StopRollbackOnMissingRollbackFile (bool?)
│       └── TargetDefaults (TargetDefaultsOptions)
│           ├── DbCommandTimeoutInSeconds       (int?, default: 20)
│           ├── DbCommandMaxRetries             (int?, default: 0)
│           └── DbCommandWaitTimeInMsBeforeRetry (int?, default: 250)
├── Products[] (ProductOptions)
│   ├── Alias                           (string, required, regex: ^(?=.{1,50}$)[\p{L}\p{N}_]+$)
│   ├── MigrationFilesRootDirectory     (string, required, validated: directory must exist)
│   ├── MigrationErrorAction            (string?, MigrationErrorAction enum)
│   ├── RollbackErrorAction             (string?, RollbackErrorAction enum)
│   ├── MigrationFilesExtension         (string?)
│   ├── MigrationRollbackFilesPreExtension (string?)
│   ├── MigrationFilesEncoding          (string?)
│   ├── RequireRollbackFile             (bool?)
│   ├── StopRollbackOnMissingRollbackFile (bool?)
│   ├── UseCliToolAlias                     (string?)
│   ├── TargetGroupMigrationOrder       (string?, comma-separated TargetGroup aliases)
│   └── TargetGroups[] (TargetGroupOptions)
│       ├── Alias                       (string, required, regex: ^(?=.{1,50}$)[\p{L}\p{N}_]+$)
│       ├── DatabaseType                (string, required)
│       ├── TargetMigrationOrder              (string?, TargetMigrationOrder enum)
│       ├── HashValidationScope         (string?, HashValidationScope enum)
│       ├── StopRollbackOnMissingRollbackFile (bool?)
│       ├── UseCliToolAlias                 (string?)
│       └── Targets[] (TargetOptions)
│           ├── Alias                   (string, required, regex: ^(?=.{1,50}$)[\p{L}\p{N}_]+$)
│           ├── ConnectionString        (string, required)
│           ├── DbCommandTimeoutInSeconds       (int?, default: 20)
│           ├── DbCommandMaxRetries             (int?, default: 0)
│           ├── DbCommandWaitTimeInMsBeforeRetry (int?, annotation default: 500, effective default: 250 via TargetDefaults)
│           ├── UseCliToolAlias             (string?)
│           └── CliToolParameters       (Dictionary<string, string>?)
└── CliTools[] (CliToolOptions)
    ├── Alias                           (string, required, regex: ^(?=.{1,50}$)[\p{L}\p{N}_\-]+$)
    ├── ExecutablePath                  (string, required)
    ├── ArgumentTemplate                (string, required)
    ├── InputMode                       (string?, CliToolInputMode enum, default: File)
    ├── SuccessExitCodes                (string[]?, default: ["0"], range notation supported)
    └── CliToolTimeoutInSeconds         (int?, default: 120)
```

Namespace: `Raycoon.RayMigrator.Core.Configuration.Options`

### Enum Values

Enum properties are stored as strings in JSON and parsed with `Enum.TryParse`. Each options class exposes a computed `*Enum` property (e.g., `MigrationErrorActionEnum`) with lazy parsing.

| Enum | Values |
|------|--------|
| `MigrationErrorAction` | `Terminate` (10), `Rollback` (20), `RollbackErrorOnly` (21), `RollbackRelease` (22), `Ignore` (30) |
| `RollbackErrorAction` | `Terminate` (10), `Ignore` (30) |
| `TargetMigrationOrder` | `Simultaneously` (1), `Successively` (2) |
| `HashValidationScope` | `File` (1), `SqlBlocks` (2), `Disabled` (3) |
| `CliToolInputMode` | `Undefined` (0), `File` (1), `Stdin` (2) |

Namespace: `Raycoon.RayMigrator.Core.Configuration.Enums`

### Defaults Inheritance

Product, TargetGroup, and Target settings inherit from their corresponding defaults via `ProductDefaultsPostConfigureOptions` (an `IPostConfigureOptions<RayMigratorOptions>` implementation). This runs after configuration binding and copies default values into any product/targetgroup/target property that was not explicitly set.

The class also exposes a `static MergeDefaults(RayMigratorOptions)` method that can be called directly by external code (e.g., from RayMigrator Studio) after building `RayMigratorOptions` from an alternative source (bypassing the `IPostConfigureOptions` pipeline).

The inheritance cascade:

- **ProductDefaults** -> each **Product**: `MigrationErrorAction`, `RollbackErrorAction`, `MigrationFilesExtension`, `MigrationRollbackFilesPreExtension`, `MigrationFilesEncoding`, `RequireRollbackFile`, `StopRollbackOnMissingRollbackFile`, `UseCliToolAlias`
- **Product** -> each **TargetGroup**: `UseCliToolAlias`
- **TargetGroupDefaults** -> each **TargetGroup**: `TargetMigrationOrder`, `HashValidationScope`, `StopRollbackOnMissingRollbackFile`
- **TargetGroup** -> each **Target**: `UseCliToolAlias`
- **TargetDefaults** -> each **Target**: `DbCommandTimeoutInSeconds`, `DbCommandMaxRetries`, `DbCommandWaitTimeInMsBeforeRetry`

```json
{
  "RayMigrator": {
    "ProductDefaults": {
      "MigrationErrorAction": "Terminate",
      "RollbackErrorAction": "Terminate",
      "MigrationFilesExtension": "sql",

      "TargetGroupDefaults": {
        "TargetMigrationOrder": "Successively",
        "HashValidationScope": "File",

        "TargetDefaults": {
          "DbCommandTimeoutInSeconds": 20,
          "DbCommandMaxRetries": 0
        }
      }
    },

    "Products": [{
      "Alias": "MyProduct",
      // Inherits from ProductDefaults unless overridden
      "MigrationErrorAction": "Rollback",  // Override
      // RollbackErrorAction inherited as "Terminate"

      "TargetGroups": [{
        "Alias": "Backend",
        // Inherits from TargetGroupDefaults

        "Targets": [{
          "Alias": "MainDB",
          // Inherits from TargetDefaults
          "DbCommandTimeoutInSeconds": 120  // Override
        }]
      }]
    }]
  }
}
```

### CLI Tool Execution Mode

As an alternative to the built-in DAL (Data Access Layer), RayMigrator can execute migration SQL files using external CLI tools (e.g., `sqlcmd`, `psql`, `mysql`, `mariadb`, `sqlite3`). This is configured via the `CliTools` array at the `RayMigratorOptions` root level and the `UseCliToolAlias` property at various levels of the options hierarchy.

`CliToolOptions` defines a reusable CLI tool configuration:

- `Alias`: Unique identifier referenced by `UseCliToolAlias` at any level
- `ExecutablePath`: Path to the CLI executable (absolute or relative/in PATH)
- `ArgumentTemplate`: Command-line template with placeholders (`{FilePath}` for the migration file, plus custom placeholders resolved from `CliToolParameters` on the Target)
- `InputMode` (`CliToolInputMode`): `File` (pass file path as argument) or `Stdin` (pipe file content via standard input)
- `SuccessExitCodes`: Whitelist of exit code expressions that indicate success. Supports single values (`"0"`), closed ranges (`"1..5"`), and open ranges (`"10.."`, `"..-1"`). Any exit code not matched is treated as failure.
- `CliToolTimeoutInSeconds`: Maximum execution time (default: 120)

`UseCliToolAlias` is inherited through the configuration hierarchy: `ProductDefaults` -> `Product` -> `TargetGroup` -> `Target`. It can also be overridden per directory via migsettings files or per file via TOML metadata. When set, the referenced CLI tool is used instead of the DAL for executing migration SQL. When null or empty, the built-in DAL is used (default behavior).

`CliToolParameters` on `TargetOptions` provides key-value pairs for placeholder substitution in the `ArgumentTemplate`. Values support `{ENV:VAR}` replacement.

Source: `Raycoon.RayMigrator.Core/Configuration/Options/RayMigratorOptions.cs`

See [CLI Tools Options](../06-configuration-reference/cli-tools-options.md) for the full `CliToolOptions` property reference, validation rules, and example configurations.

## Environment Variable Placeholders

All string configuration values support `{ENV:VARIABLE_NAME}` syntax. Placeholders are resolved at runtime by `EnvironmentVariableReplacer` before options binding. The replacer walks the `IConfigurationSection` tree recursively and replaces all matches of the pattern `{ENV:\w+}` with the corresponding `System.Environment.GetEnvironmentVariable` value.

A single configuration value can contain multiple placeholders (e.g., `"Server={ENV:DB_HOST};Database={ENV:DB_NAME}"`). If an environment variable does not exist, its placeholder is replaced with `null` (no exception at replacement time), but the startup process logs an error and aborts.

```json
{
  "RayMigrator": {
    "Repository": {
      "ConnectionString": "{ENV:REPO_CONNECTION}"
    }
  }
}
```

Namespace: `Raycoon.RayMigrator.Core.Configuration.Replacer`

For full details on placeholder syntax, resolution, and setting environment variables across platforms, see [Environment Variables](../06-configuration-reference/environment-variables.md).

## Loading Process

### Standalone Mode (JSON Config)

```mermaid
sequenceDiagram
    participant CLI as CommandLineConfiguration
    participant App as Program.Main
    participant Env as EnvironmentResolver
    participant Src as JsonOptionsSource
    participant EnvRepl as EnvironmentVariableReplacer
    participant Pipe as DirectModePipeline
    participant Opt as Options Binder
    participant PC as ProductDefaultsPostConfigureOptions
    participant Ctx as MigrationContext

    CLI->>App: Parse args → RayMigratorConsoleOptions
    App->>Env: EnvironmentResolver.Resolve(consoleOptions, assemblyInfo)
    Env-->>App: environment, environmentOrigin

    App->>Src: JsonOptionsSource.LoadAsync(product, environment)
    Src->>Src: Load appsettings.json + appsettings.{Environment}.json + product-specific files
    Src->>Src: Merge configurations (later overrides earlier)
    Src->>EnvRepl: Resolve {ENV:*} placeholders in RayMigrator section
    EnvRepl->>EnvRepl: Replace with environment variable values
    EnvRepl-->>Src: Replaced configuration + replacement metadata
    Src-->>App: OptionsSourceResult (IConfiguration + RayMigrator section + replacement metadata)

    App->>Pipe: DirectModePipeline.ExecuteAsync(...)
    Pipe->>Pipe: Log and validate environment variable replacements (abort if unresolved)

    Pipe->>Opt: Bind "RayMigrator" section to RayMigratorOptions
    Opt-->>Pipe: Strongly-typed options

    Pipe->>PC: ProductDefaultsPostConfigureOptions.PostConfigure
    PC->>PC: MergeDefaults: copy defaults to Products/TargetGroups/Targets
    PC-->>Pipe: Options with inherited defaults

    Pipe->>Ctx: Create MigrationContext(options, consoleOptions, version)
```

### ManagedLocal Mode (Admin-DB)

> **Implementation Status**: The `OperatingMode.ManagedLocal` enum value and `AdminDbOptions` configuration class exist in Engine's Core layer (part of the NuGet contract), but the Admin-DB operating mode is implemented in **RayMigrator Studio**, not in Engine. When Studio uses ManagedLocal mode, it supplies a pre-built `RayMigratorOptions` via the `PreBuiltOptions` field of `OptionsSourceResult`, and the `DirectModePipeline` handles the rest of the lifecycle identically to Standalone mode.

## Options Source Abstraction

Configuration loading is abstracted behind the `IOptionsSource` interface (namespace `Raycoon.RayMigrator.Core.Configuration.Sources`), allowing the same `DirectModePipeline` to handle different configuration sources:

- **`JsonOptionsSource`** (Pipeline project) -- Loads from `appsettings.json` file hierarchy, resolves `{ENV:*}` placeholders, returns an `OptionsSourceResult` with `HostConfiguration` set for DI binding. This is the only implementation in Engine.

The `OptionsSourceResult` (namespace `Raycoon.RayMigrator.Core.Configuration.Sources`) carries all data needed by the `DirectModePipeline`:

- `RayMigratorConfigSection` (`IConfigurationSection`, required) -- Used for Serilog configuration reading and verbose output
- `PreBuiltOptions` (`RayMigratorOptions?`) -- Pre-built options supplied by the caller; when null, options are resolved via DI binding from the `HostConfiguration`
- `ReplacedEnvironmentVariables` (`List<EnvironmentVariableWithMetadata>`) -- Environment variables replaced during loading
- `HostConfiguration` (`IConfigurationRoot?`) -- Configuration root to add to the DI host builder (JSON mode)
- `ModeName` (`string`, required) -- Display name for log messages (e.g., "Standalone mode")
- `ConfigFileDiagnostics` (`List<(string Filename, bool Found)>?`) -- Configuration file search diagnostics for error messages (JSON mode only)

## Validation

Options are validated at startup via three layered mechanisms, wired up in `DirectModePipeline.cs`:

1. **Data annotation validation** (`.ValidateDataAnnotations()`): property-level attributes on `RayMigratorOptions` and its subtypes — `[Required]`, `[RegularExpression]`, `[ValidateObjectMembers]`, `[ValidateEnumeratedItems]`, plus custom attributes (`[RayRangeInt]`, `[RayEnum]`, `[RayEncoding]`, `[RayConnectionString]`, `[RayDirectoryExists]`). These cover single-property shape checks, including WASM-unsafe ones like ADO.NET connection-string parsing and filesystem existence.
2. **Shared rule catalog** (`RayMigratorOptionsValidator`, an `IValidateOptions<RayMigratorOptions>` implementation): delegates to the `RuleCatalog` in `Raycoon.RayMigrator.Validation` via `OptionsValidationInputAdapter`. Covers all cross-field and cross-section rules (duplicate aliases, CLI-tool parameter completeness, schema requirements, default-cascade completeness, connection-string hygiene). The same catalog runs inside the ConfigWizard's Overview panel, so engine startup and wizard preview agree on what is valid.
3. **`.ValidateOnStart()`**: forces both mechanisms above to run during `host.Build()` rather than lazily on first `IOptions<T>.Value`. A misconfigured production deployment fails fast with `OptionsValidationException` carrying rule-code-prefixed messages (`[RULE_3_8] Products > MyApp > ... : ...`) instead of surfacing the error on the first DB call.

Warnings (non-blocking issues from the shared catalog) are emitted to the static `Serilog.Log` channel at `Warning` level before validation decides pass/fail; only errors cause `host.Build()` to throw.

**Admin-DB / Studio mode**: the `PreBuiltOptions` branch in `DirectModePipeline` (used by RayMigrator-Studio's API layer) registers `RayMigratorOptions` via `Options.Create(...)` and therefore does **not** invoke `IValidateOptions<T>`. This is intentional — pre-built options from Studio's `AdminDbOptionsSource` are trusted. If future Studio code needs validation, it should call `RayMigratorOptionsValidator.Validate(null, preBuilt)` explicitly.

Namespaces:
- Custom attributes: `Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes`
- Engine-side adapter: `Raycoon.RayMigrator.Core.Configuration.Validation.OptionsValidationInputAdapter`
- Shared catalog: `Raycoon.RayMigrator.Validation` (zero dependencies, WASM-safe)

For the authoritative list of all rule codes, their severities, and example messages, see [Appendix: Validation Rules](../appendix/validation-rules.md). For a broader overview of where validation attributes sit in the configuration hierarchy, see [Configuration Hierarchy — Validation](../06-configuration-reference/appsettings-hierarchy.md).

## Related Documentation

- [Configuration Hierarchy](../06-configuration-reference/appsettings-hierarchy.md) - File precedence and merge rules
- [Repository Options](../06-configuration-reference/repository-options.md)
- [Product Options](../06-configuration-reference/product-options.md)
- [Settings Inheritance Overview](../06-configuration-reference/settings-inheritance-overview.md) - Full inheritance chain including migsettings
- [CLI Tools Options](../06-configuration-reference/cli-tools-options.md) - External CLI tool configuration
- [Environment Variables](../06-configuration-reference/environment-variables.md)
- [MigSettings Files](../07-migration-files/migsettings-files.md) - Directory-level TOML defaults (`migsettings.txt`)
- [Migration Context](migration-context.md)
