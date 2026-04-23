# Config-Wizard: Validation Reference

The wizard performs real-time validation as you edit. Validation runs on every model change and powers the badges in the Overview page.

## Rule Catalog

All validation rules (RULE_1_1 … RULE_8_3) are defined once in the shared library `Raycoon.RayMigrator.Validation` and run in both the wizard and the engine. The **authoritative catalog — rule IDs, severities, what each rule checks, example messages, and breaking-change notes — lives in [Docs/appendix/validation-rules.md](../appendix/validation-rules.md)**.

This page documents only the **wizard-specific surface**: entry points, capability flags, the Overview badge, and wizard-only checks that do not live in the shared catalog.

## Validation Entry Point

The Blazor Web wizard uses `Raycoon.RayMigrator.ConfigWizard.Core.Services.ConfigurationValidator`. It passes `ValidationCapability.Structural` for WASM-safe validation only. The `Filesystem` and `AdoNetParsing` capabilities exist in Core for potential future use but are not active in WASM.

`ConfigurationValidator.ValidateAll` has two overloads:

```csharp
// Backward-compatible — runs with Structural capability only
public static WizardValidationResult ValidateAll(ConfigurationModel model)

// Full control — pass explicit capabilities
public static WizardValidationResult ValidateAll(ConfigurationModel model, ValidationCapability capabilities)
```

`ValidationCapability` is a `[Flags]` enum in `Raycoon.RayMigrator.ConfigWizard.Core.Models`:

| Value | Flag | Description |
|-------|------|-------------|
| `Structural` | `0` | Pure in-memory / cross-field rules. Always available, including in WASM. |
| `Filesystem` | `1` | Rules that need filesystem access (e.g. path existence checks). |
| `AdoNetParsing` | `2` | Rules that need ADO.NET connection string parsing via `DbConnectionStringBuilder`. |

The `Filesystem` flag gates `MigrationFilesRootDirectory` existence checks in `FilesystemChecks`. The `AdoNetParsing` flag gates strict connection string parsing in `AdoNetChecks` (a regex heuristic is used otherwise).

## Result Shape

`ValidateAll` returns a `WizardValidationResult` — the Blazor UI consumes this directly:

```csharp
public class WizardValidationResult
{
    public List<ValidationEntry> Errors { get; set; }   // Blocks save when non-empty
    public List<ValidationEntry> Warnings { get; set; } // Does not block save
    public bool IsValid => Errors.Count == 0;
    public int TotalIssues => Errors.Count + Warnings.Count;
}

public class ValidationEntry
{
    public string Path { get; set; }      // e.g. "Products > MyApp > TargetGroups > Backend"
    public string Message { get; set; }   // Human-readable explanation
    public ValidationSeverity Severity { get; set; }
    public string? Code { get; set; }     // Rule code from the shared catalog, e.g. "RULE_3_8"
}
```

`Code` is populated for every entry that originates from the shared rule catalog; it is null for the few wizard-only checks listed below.

## Section-Scoped API

Individual section validators are available for fine-grained UI panels:

- `ValidateRepository(repo)`
- `ValidateDatabaseLogging(dbLog)`
- `ValidateProductDefaults(defaults)`
- `ValidateProduct(product, prefix)`
- `ValidateTargetGroup(tg, prefix)`
- `ValidateTarget(target, prefix)`
- `ValidateCliTools(cliTools)`
- `ValidateCliTool(tool, prefix)`
- `ValidateUseCliToolAliasReferences(model)`

All take an optional `WizardValidationResult? existing` parameter to accumulate into a shared result. Under the hood they build a minimal `ValidationInput` tree, run the shared rule catalog, and filter issues by path prefix so callers only see issues relevant to their section.

## How the Catalog Runs

```text
ConfigurationModel
      │
      ▼
WizardValidationInputAdapter.ToInput   (resolves OverridableValue<T> cascade)
      │
      ▼
ValidationInput
      │
      ▼
RuleCatalog.RunAll                      (shared library, WASM-safe)
      │
      ▼
ValidationReport
      │
      ▼
ValidationReportToWizardResultMapper    (translates Issue → ValidationEntry)
      │
      ▼
WizardValidationResult   +   WizardOnlyChecks   +   FilesystemChecks (if capability)   +   AdoNetChecks (if capability)
```

## Wizard-Only Checks

A small set of checks lives outside the shared catalog because they are meaningful only inside the wizard UI. These never carry a `Code` value.

| Scope | Check |
|-------|-------|
| Repository | `DatabaseType` must be one of `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite`. Numeric fields (`DbCommandTimeoutInSeconds`, `DbCommandMaxRetries`, `DbCommandWaitTimeInMsBeforeRetry`) must be ≥ 0. |
| DatabaseLogging | Same `DatabaseType` set; `MinimumLevel` must be one of `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Sqlite + non-empty `SchemaName` is a warning. |
| ProductDefaults | `MigrationErrorAction` must be one of `Terminate`, `Rollback`, `RollbackErrorOnly`, `RollbackRelease`, `Ignore`. `RollbackErrorAction` must be one of `Terminate`, `Ignore`. File extensions must match `^[a-zA-Z_]+$`. `MigrationFilesEncoding` must resolve via `Encoding.GetEncoding(...)`. |
| Product | `Alias` must match `^(?=.{1,50}$)[\p{L}\p{N}_]+$`. `MigrationFilesRootDirectory` must be non-empty. `TargetGroups` must contain at least one entry. |
| TargetGroup | Same alias pattern; `DatabaseType` required; `Targets` must contain at least one entry. Warns if `TargetMigrationOrder` is overridden on a single-target TargetGroup. |
| Target | Same alias pattern; `ConnectionString` required. |
| Serilog | Warns if `MinimumLevelDefault` is outside the known Serilog set; warns if `WriteTo` is empty. |
| CliTool | Alias pattern (`^(?=.{1,50}$)[\p{L}\p{N}_\-]+$`). `ExecutablePath` / `ArgumentTemplate` required. `InputMode` must be `File` or `Stdin`. Timeout must be > 0. |
| Products | File-role-aware: when no products are defined and the file role is `Base` or `Environment`, this is a warning; otherwise an error. |

Implementation: `Raycoon.RayMigrator.ConfigWizard.Core.Services.WizardOnlyChecks`.

## Overview Rendering

The Overview page renders `WizardValidationResult.Errors` and `Warnings` as separate lists with the path as a breadcrumb and the rule code (when present) as a compact badge. Search the code in [Docs/appendix/validation-rules.md](../appendix/validation-rules.md#rule-index) to get the full description.

## ENV Variable Placeholders

Connection strings containing `{ENV:VARIABLE_NAME}` skip both regex and ADO.NET syntax validation — the wizard trusts them as valid placeholders resolved at runtime by the engine.

Connection strings without `{ENV:}` are validated with a regex heuristic (at least one `key=value` pair) when `AdoNetParsing` is not in the capability set. This is intentionally lenient to accommodate third-party drivers.

## Related Documentation

- **[Appendix: Validation Rules](../appendix/validation-rules.md)** — canonical rule catalog (shared with the engine).
- [Services Reference: ConfigurationValidator](./services.md#configurationvalidator)
- [Architecture: WizardValidationResult](./architecture.md#wizardvalidationresult)
- [Configuration Reference](../06-configuration-reference/appsettings-hierarchy.md)
