# Config-Wizard Architecture

## Projects

The Config-Wizard ecosystem consists of two projects:

- **`Raycoon.RayMigrator.ConfigWizard.Core`** — shared domain library (zero NuGet/project dependencies). Provides all models, services, and validation logic. Used by the Web wizard and independently unit-tested.
- **`Raycoon.RayMigrator.ConfigWizard.Web`** — Blazor WASM standalone wizard. Consumes Core models and services; adds Blazor components, `WizardStateService`, and Web-specific infrastructure services.

## Models

### ConfigurationModel

The central mutable in-memory representation of a RayMigrator configuration file lives in the Core project as `ConfigurationModel`. It is IO-free and is used by the Blazor Web wizard:

```
ConfigurationModel  (Raycoon.RayMigrator.ConfigWizard.Core)
├── RepositoryModel Repository
│   ├── string DatabaseType           (default: "SqlServer")
│   ├── string ConnectionString       (default: "")
│   ├── string SchemaName             (default: "ray")
│   ├── string TableBaseName          (default: "")
│   ├── int DbCommandTimeoutInSeconds (default: 60)
│   ├── int DbCommandMaxRetries       (default: 100)
│   └── int DbCommandWaitTimeInMsBeforeRetry (default: 250)
├── DatabaseLoggingModel? DatabaseLogging
│   ├── string DatabaseType           (default: "SqlServer")
│   ├── string ConnectionString       (default: "")
│   ├── string SchemaName             (default: "ray")
│   ├── string TableBaseName          (default: "")
│   ├── string MinimumLevel           (default: "Information")
│   └── int DbCommandTimeoutInSeconds (default: 20)
├── ProductDefaultsModel ProductDefaults
│   ├── string MigrationErrorAction           (default: "Terminate")
│   ├── string RollbackErrorAction            (default: "Terminate")
│   ├── string MigrationFilesExtension        (default: "sql")
│   ├── string MigrationRollbackFilesPreExtension (default: "rollback")
│   ├── string MigrationFilesEncoding         (default: "UTF-8")
│   ├── bool RequireRollbackFile              (default: true)
│   ├── bool StopRollbackOnMissingRollbackFile (default: true)
│   ├── string? UseCliToolAlias
│   └── TargetGroupDefaultsModel TargetGroupDefaults
│       ├── string TargetMigrationOrder             (default: "Successively")
│       ├── string HashValidationScope        (default: "File")
│       ├── bool StopRollbackOnMissingRollbackFile (default: true)
│       └── TargetDefaultsModel TargetDefaults
│           ├── int DbCommandTimeoutInSeconds (default: 20)
│           ├── int DbCommandMaxRetries       (default: 0)
│           └── int DbCommandWaitTimeInMsBeforeRetry (default: 250)
├── List<ProductModel> Products
│   └── ProductModel
│       ├── string Alias
│       ├── string MigrationFilesRootDirectory
│       ├── OverridableValue<string> MigrationErrorAction
│       ├── OverridableValue<string> RollbackErrorAction
│       ├── OverridableValue<string> MigrationFilesExtension
│       ├── OverridableValue<string> MigrationRollbackFilesPreExtension
│       ├── OverridableValue<string> MigrationFilesEncoding
│       ├── OverridableValue<bool> RequireRollbackFile
│       ├── OverridableValue<bool> StopRollbackOnMissingRollbackFile
│       ├── OverridableValue<string> UseCliToolAlias
│       ├── Dictionary<string,string>? CliToolParameters   (wizard-only, propagated to Targets on serialization)
│       ├── string? TargetGroupMigrationOrder
│       ├── bool GenerateScaffold         (UI-only, not serialized)
│       └── List<TargetGroupModel> TargetGroups
│           └── TargetGroupModel
│               ├── string Alias
│               ├── string DatabaseType
│               ├── OverridableValue<string> TargetMigrationOrder
│               ├── OverridableValue<string> HashValidationScope
│               ├── OverridableValue<string> UseCliToolAlias
│               ├── Dictionary<string,string>? CliToolParameters   (wizard-only, propagated to Targets on serialization)
│               ├── OverridableValue<bool> StopRollbackOnMissingRollbackFile
│               └── List<TargetModel> Targets
│                   └── TargetModel
│                       ├── string Alias
│                       ├── string ConnectionString
│                       ├── OverridableValue<int> DbCommandTimeoutInSeconds
│                       ├── OverridableValue<int> DbCommandMaxRetries
│                       ├── OverridableValue<int> DbCommandWaitTimeInMsBeforeRetry
│                       ├── OverridableValue<string> UseCliToolAlias
│                       └── Dictionary<string,string>? CliToolParameters
├── SerilogModel Serilog
│   ├── string MinimumLevelDefault    (default: "Information")
│   ├── Dictionary<string, string> MinimumLevelOverrides
│   └── List<SerilogSinkModel> WriteTo
│       └── SerilogSinkModel
│           ├── string Name           (default: "Console")
│           └── Dictionary<string, string> Args
├── List<CliToolModel> CliTools
├── bool IsModified
├── string? FilePath
├── ConfigFileRole? FileRole
└── JsonNode? PreservedDocument
```

`CliToolModel` holds `Alias`, `ExecutablePath`, `ArgumentTemplate`, `InputMode` (`"File"` or `"Stdin"`, default: `"File"`), `SuccessExitCodes` (`List<string>`, default: `["0"]`), and `CliToolTimeoutInSeconds` (default: `120`).

**Round-trip safety**: `LoadFromJson` stores a deep clone of the full parsed document in `model.PreservedDocument`. When `ToJson` is called, it starts from the preserved document and only replaces the managed sections. The Core serializer manages 6 sections: `Repository`, `DatabaseLogging`, `ProductDefaults`, `Products`, `Serilog`, `CliTools`. Unknown keys at the `RayMigrator` level (e.g. `AdminDb`, `ApiUrl`) are preserved unchanged.

### OverridableValue&lt;T&gt;

Used for all inheritable properties at Product, TargetGroup, and Target level:

```csharp
public class OverridableValue<T>
{
    public bool IsOverridden { get; set; }
    public T? Value { get; set; }
    public T GetEffectiveValue(T defaultValue);
}
```

When `IsOverridden` is false or `Value` is null, `GetEffectiveValue` returns the `defaultValue` from the parent level. The UI renders overridden values with an active "Override" checkbox.

### ConfigFileRole

`ConfigFileRole` identifies a file's position in the 4-level hierarchy:

| Enum Value | Numeric | File Pattern |
|------------|---------|--------------|
| `Base` | 1 | `appsettings.json` |
| `Environment` | 2 | `appsettings.{Environment}.json` |
| `Product` | 3 | `appsettings.{Product}.json` |
| `ProductEnvironment` | 4 | `appsettings.{Product}.{Environment}.json` |

### WizardValidationResult

Holds validation errors and warnings:

```csharp
public class WizardValidationResult
{
    public List<ValidationEntry> Errors { get; set; }
    public List<ValidationEntry> Warnings { get; set; }
    public bool IsValid => Errors.Count == 0;
    public int TotalIssues => Errors.Count + Warnings.Count;
}
```

`ValidationEntry` has `Path` (e.g. `"Products > MyProduct > TargetGroups > Backend > Alias"`), `Message`, and `ValidationSeverity` (`Error` or `Warning`).

`WizardValidationResult` provides these helper methods:

```csharp
void AddError(string path, string message)
void AddWarning(string path, string message)
void Merge(WizardValidationResult other)  // appends all errors and warnings from another result
```

## WizardState (Core — Web wizard)

`WizardState` (`Raycoon.RayMigrator.ConfigWizard.Core.Models.WizardState`) is the top-level in-memory container for the Blazor Web wizard. It holds all configuration files as separate `ConfigurationModel` instances and tracks wizard completion per combination.

```
WizardState
├── ConfigurationModel BaseModel                              (appsettings.json)
├── Dictionary<string, ConfigurationModel> EnvironmentModels  (appsettings.{Env}.json, keyed by environment name)
├── Dictionary<string, ConfigurationModel> ProductModels       (appsettings.{Product}.json, keyed by product alias)
├── Dictionary<string, ConfigurationModel> ProductEnvironmentModels  (appsettings.{Product}.{Env}.json, keyed by "{Product}.{Env}")
├── Dictionary<string, ProductEnvironmentEntry> CombinationEntries   (keyed by "{Product}.{Env}")
└── WizardSetupAnswers SetupAnswers
```

`WizardSetupAnswers` holds: `RepositoryDatabaseType`, `List<ProductSetup> Products` (each with `Alias`, `List<string> Environments`, and `List<TargetGroupSetup> TargetGroups`), `UseDatabaseLogging`, and `UseCliTools`. Each `TargetGroupSetup` has `Alias`, `DatabaseType`, and `List<string> TargetAliases`.

`ProductEnvironmentEntry` tracks wizard completion per combination:

```csharp
public class ProductEnvironmentEntry
{
    public bool WizardCompleted { get; set; }
}
```

The Hub page uses `CombinationEntries` to show TODO/DONE status for each product+environment pair.

## WizardPhase (Web wizard)

`WizardPhase` is an enum defined in `WizardStateService.cs` that drives the top-level routing in `WizardPage.razor`:

| Value | Phase | Component |
|-------|-------|-----------|
| `Start` | Welcome screen | `WelcomeMask` |
| `Hub` | Products and Environments hub | `HubPage` |
| `GuidedConfig` | Detailed Configuration (stepper with up to 6 steps: Repository, DatabaseLogging, CliTools [Expert only], ProductDefaults, ProductSettings, Serilog — scoped to one combination) | `WizardHost` |
| `Overview` | Configuration Overview (validation, matrix, file tabs, export) | `OverviewHost` |

The `ProgressIndicator` component displays three main phases (Hub → Configuration → Overview) and highlights the current one. The Welcome phase (`Start`) is rendered before any indicator state becomes relevant.

## Related Documentation

- [Services Reference](./services.md)
- [File Hierarchy](./file-hierarchy.md)
- [Overview](./overview.md)
