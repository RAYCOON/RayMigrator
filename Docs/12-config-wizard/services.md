# Config-Wizard Services Reference

All service classes are in `Raycoon.RayMigrator.ConfigWizard.Core.Services` (shared Core library) or `Raycoon.RayMigrator.ConfigWizard.Web.Services` (Web wizard). Core services are IO-free and can be unit-tested independently.

## ConfigurationSerializer

`ConfigurationSerializer` (static, Core) handles reading and writing `appsettings.json` content to/from `ConfigurationModel`.

```csharp
// Load from JSON string
ConfigurationModel LoadFromJson(string json, string? filePath = null)

// Serialize to JSON string; optional baseModel enables diff-based serialization
// (only sections that differ from baseModel are included — used for env/product-env files)
string ToJson(ConfigurationModel model, ConfigurationModel? baseModel = null, bool indented = true)

// Returns true when the diff JSON is just the empty wrapper {"RayMigrator":{}}.
// Used by callers to skip writing override files that contain no real overrides.
bool IsEmptyDiff(string json)
```

**Round-trip safety**: `LoadFromJson` stores a deep clone of the full parsed document in `model.PreservedDocument`. When `ToJson` is called, it starts from the preserved document and only replaces the managed sections. The Core serializer manages 6 sections: `Repository`, `DatabaseLogging`, `ProductDefaults`, `Products`, `Serilog`, and `CliTools`. Unknown keys at the `RayMigrator` level (e.g. `AdminDb`, `ApiUrl`) are preserved unchanged.

**Parsing behavior**: The serializer reads `OverridableValue<T>` properties by checking whether the key is present in the JSON node. If present, `IsOverridden = true` and `Value` is set. If absent, the value stays at its default `IsOverridden = false` state.

## ConfigFileMerger

`ConfigFileMerger` (static, Core) merges a chain of configuration files following RayMigrator merge semantics. IO-free; works with in-memory JSON strings.

- **JSON objects** are recursively merged (higher-priority file wins for conflicting keys)
- **Alias-keyed arrays** (`Products`, `TargetGroups`, `Targets`, `CliTools` — any array whose every element is a JSON object with a string `Alias` property) are merged by matching `Alias`. Matching items are recursively merged, override items without a base match are appended, and base items without an override match are preserved.
- **Other JSON arrays** are completely replaced (the highest-priority file's array is used)
- **Scalar values** are replaced by the higher-priority file's value

```csharp
// Merge and return a ConfigurationModel from ordered JSON strings
static ConfigurationModel MergeChain(IReadOnlyList<string> jsonStrings)

// Merge and return the merged JSON string from ordered JSON strings
static string MergeChainToJson(IReadOnlyList<string> jsonStrings, bool indented = true)

// Merge two JsonNode instances
static JsonNode? MergeJson(JsonNode? baseNode, JsonNode? overrideNode)
```

Entries that cannot be parsed are skipped silently.

## ConfigurationValidator

`ConfigurationValidator` (`Raycoon.RayMigrator.ConfigWizard.Core.Services.ConfigurationValidator`, static) validates a `ConfigurationModel` against RayMigrator's configuration rules. The Web wizard passes `ValidationCapability.Structural` for WASM-safe validation only.

```csharp
// Backward-compatible — runs with Structural capability only
WizardValidationResult ValidateAll(ConfigurationModel model)

// Full control — pass explicit capabilities
WizardValidationResult ValidateAll(ConfigurationModel model, ValidationCapability capabilities)

WizardValidationResult ValidateRepository(RepositoryModel repo,
    WizardValidationResult? existing = null, ValidationCapability capabilities = Structural)
WizardValidationResult ValidateDatabaseLogging(DatabaseLoggingModel dbLog,
    WizardValidationResult? existing = null, ValidationCapability capabilities = Structural)
WizardValidationResult ValidateProductDefaults(ProductDefaultsModel defaults,
    WizardValidationResult? existing = null)
WizardValidationResult ValidateProduct(ProductModel product, string prefix,
    WizardValidationResult? existing = null, ValidationCapability capabilities = Structural)
WizardValidationResult ValidateTargetGroup(TargetGroupModel tg, string prefix,
    WizardValidationResult? existing = null, ValidationCapability capabilities = Structural)
WizardValidationResult ValidateTarget(TargetModel target, string prefix,
    WizardValidationResult? existing = null, ValidationCapability capabilities = Structural)
WizardValidationResult ValidateCliTools(List<CliToolModel> cliTools,
    WizardValidationResult? existing = null)
WizardValidationResult ValidateCliTool(CliToolModel tool, string prefix,
    WizardValidationResult? existing = null)
WizardValidationResult ValidateUseCliToolAliasReferences(ConfigurationModel model,
    WizardValidationResult? existing = null)
```

**Accepted values**:

| Field | Valid Values |
|-------|-------------|
| `DatabaseType` | `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite` (warns if `SchemaName` is set for Sqlite) |
| `MigrationErrorAction` | `Terminate`, `Rollback`, `RollbackErrorOnly`, `RollbackRelease`, `Ignore` |
| `RollbackErrorAction` | `Terminate`, `Ignore` |
| `TargetMigrationOrder` | `Simultaneously`, `Successively` |
| `HashValidationScope` | `File`, `SqlBlocks`, `Disabled` |
| `MinimumLevel` (DatabaseLogging) | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None` |
| `MinimumLevel` (Serilog) | `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |

**Alias rules**: Letters, numbers, and underscores only, 1–50 characters (`^(?=.{1,50}$)[\p{L}\p{N}_]+$`). CLI tool aliases additionally allow hyphens.

**Products validation**: When the file role is `Base` or `Environment` and no products are defined, a warning (not an error) is issued, since products are typically in product-specific files.

**Connection string validation**: Skipped if the value contains `{ENV:`. By default a regex heuristic is used — requires at least one `key=value` pair. When `ValidationCapability.AdoNetParsing` is active, `WizardOnlyChecks.ValidateConnectionString` switches to strict ADO.NET `DbConnectionStringBuilder` parsing. The `AdoNetParsing` capability is not passed by the Web wizard (WASM).

**Directory existence**: When `ValidationCapability.Filesystem` is active, `ValidateProduct` warns if `MigrationFilesRootDirectory` does not exist on disk via `FilesystemChecks.ValidateProductDirectory`. The `Filesystem` capability is not passed by the Web wizard (WASM).

## InheritanceResolver

`InheritanceResolver` (static, Core) resolves effective configuration values by applying the inheritance chain: `ProductDefaults` → `Product` → `TargetGroup` → `Target`.

```csharp
// Product-level effective values
string GetEffectiveMigrationErrorAction(ProductModel product, ProductDefaultsModel defaults)
string GetEffectiveRollbackErrorAction(ProductModel product, ProductDefaultsModel defaults)
string GetEffectiveMigrationFilesExtension(ProductModel product, ProductDefaultsModel defaults)
string GetEffectiveMigrationRollbackFilesPreExtension(ProductModel product, ProductDefaultsModel defaults)
string GetEffectiveMigrationFilesEncoding(ProductModel product, ProductDefaultsModel defaults)
bool GetEffectiveRequireRollbackFile(ProductModel product, ProductDefaultsModel defaults)
bool GetEffectiveStopRollbackOnMissingRollbackFile(ProductModel product, ProductDefaultsModel defaults)

// TargetGroup-level effective values
string GetEffectiveTargetMigrationOrder(TargetGroupModel tg, ProductDefaultsModel defaults)
string GetEffectiveHashValidationScope(TargetGroupModel tg, ProductDefaultsModel defaults)
bool GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup(TargetGroupModel tg, ProductDefaultsModel defaults)

// Target-level effective values
int GetEffectiveTimeout(TargetModel target, ProductDefaultsModel defaults)
int GetEffectiveMaxRetries(TargetModel target, ProductDefaultsModel defaults)
int GetEffectiveWaitTime(TargetModel target, ProductDefaultsModel defaults)

// UseCliToolAlias cascade resolution (4-level: ProductDefaults -> Product -> TargetGroup -> Target)
string? GetEffectiveUseCliToolAlias(TargetModel target, TargetGroupModel tg, ProductModel product, ProductDefaultsModel defaults)

// CliToolParameters cascade resolution (3-level: Target -> TargetGroup -> Product)
// Returns the first non-null, non-empty dictionary found, or null.
Dictionary<string, string>? GetEffectiveCliToolParameters(
    TargetModel target, TargetGroupModel tg, ProductModel product)

// Returns the source label ("Target", "TargetGroup", or "Product") for the effective CliToolParameters,
// or null when no level supplies them.
string? GetCliToolParametersSourceLabel(
    TargetModel target, TargetGroupModel tg, ProductModel product)

// All effective values for a target with source annotations
List<EffectiveConfigEntry> GetEffectiveTargetConfig(
    TargetModel target, TargetGroupModel tg, ProductModel product, ProductDefaultsModel defaults)
```

`GetEffectiveTargetConfig` includes `UseCliToolAlias`, `CliToolParameters`, `StopRollbackOnMissingRollbackFile (TargetGroup)`, and `StopRollbackOnMissingRollbackFile (Product)` in the output. Source labels include `"override at Target"`, `"override at TargetGroup"`, `"override at Product"`, `"override"`, `"default"`, `"not set"`, `"target"`, and `"target-group"`.

`EffectiveConfigEntry` (in `Raycoon.RayMigrator.ConfigWizard.Core.Models`) has read-only `Property`, `Value`, and `Source` string members and a public constructor that takes all three.

## ContextHelpProvider (Core)

`ContextHelpProvider` (static, Core) provides multilingual help text via `.resx` resource files and returns `SectionHelp?` and `FieldHelp?` records (from `HelpModels.cs`). It accepts an optional `CultureInfo` parameter to select the language (defaults to `CultureInfo.CurrentUICulture`).

```csharp
SectionHelp? GetSectionHelp(string sectionKey, CultureInfo? culture = null)
FieldHelp? GetFieldHelp(string fieldKey, CultureInfo? culture = null)
IReadOnlyList<string> GetAllSectionKeys()
IReadOnlyList<string> GetAllFieldKeys()
```

`GetFieldHelp` delegates to `JsonPathRegistry.GetPathInfo(fieldKey)` and includes the result as the `JsonPath` parameter of the returned `FieldHelp` record. The `FieldHelp` record signature is:

```csharp
public record FieldHelp(
    string Title,
    string Description,
    string? Examples = null,
    string? ValidValues = null,
    string? DefaultValue = null,
    string? InheritanceNote = null,
    JsonPathInfo? JsonPath = null
);
```

`JsonPathInfo` is also defined in `HelpModels.cs`:

```csharp
public record JsonPathInfo(
    string ConfigPath,
    IReadOnlyList<string>? InheritedByPaths = null,
    string? InheritedFromPath = null
);
```

`SectionHelp` is a simple record `(string Title, string Description)`.

Section keys include: `Welcome`, `Root`, `Repository`, `DatabaseLogging`, `ProductDefaults`, `Product`, `TargetGroup`, `Target`, `Serilog`, `CliTools`, `Products`, `TargetGroups`, `Targets`.

Field keys follow the pattern `"Section_FieldName"` (underscore separator). The full list of defined keys:

Repository: `"Repository_DatabaseType"`, `"Repository_ConnectionString"`, `"Repository_SchemaName"`, `"Repository_TableBaseName"`, `"Repository_Timeout"`, `"Repository_MaxRetries"`, `"Repository_Wait"`

DatabaseLogging: `"DatabaseLogging_Enable"`, `"DatabaseLogging_DatabaseType"`, `"DatabaseLogging_ConnectionString"`, `"DatabaseLogging_SchemaName"`, `"DatabaseLogging_MinimumLevel"`, `"DatabaseLogging_Timeout"`, `"DatabaseLogging_TableBaseName"`

ProductDefaults: `"ProductDefaults_MigrationErrorAction"`, `"ProductDefaults_RollbackErrorAction"`, `"ProductDefaults_MigrationFilesExtension"`, `"ProductDefaults_RollbackFilesPreExtension"`, `"ProductDefaults_MigrationFilesEncoding"`, `"ProductDefaults_RequireRollbackFile"`, `"ProductDefaults_StopRollbackOnMissingRollbackFile"`, `"ProductDefaults_TargetMigrationOrder"`, `"ProductDefaults_HashValidationScope"`, `"ProductDefaults_UseCliToolAlias"`

Product: `"Product_Alias"`, `"Product_MigrationFilesRootDirectory"`, `"Product_UseCliToolAlias"`, `"Product_TargetGroupMigrationOrder"`

TargetGroup: `"TargetGroup_Alias"`, `"TargetGroup_DatabaseType"`, `"TargetGroup_TargetMigrationOrder"`, `"TargetGroup_HashValidationScope"`, `"TargetGroup_UseCliToolAlias"`

Target: `"Target_Alias"`, `"Target_ConnectionString"`, `"Target_Timeout"`, `"Target_MaxRetries"`, `"Target_Wait"`, `"Target_UseCliToolAlias"`, `"Target_CliToolParameters"`

CliTool: `"CliTool_Alias"`, `"CliTool_ExecutablePath"`, `"CliTool_ArgumentTemplate"`, `"CliTool_InputMode"`, `"CliTool_SuccessExitCodes"`, `"CliTool_Timeout"`

Serilog: `"Serilog_MinimumLevel"`, `"Serilog_SinkName"`, `"Serilog_OverrideSource"`, `"Serilog_OverrideLevel"`, `"Serilog_SinkArgs"`

Concept: `"Concept_Environment"`, `"Concept_TargetGroup"`, `"Concept_Target"`

## JsonPathRegistry (Core)

`JsonPathRegistry` (static, Core) maps field help keys to their corresponding JSON configuration paths. It is used internally by `ContextHelpProvider.GetFieldHelp` to populate the `JsonPath` field of the returned `FieldHelp` record, enabling the wizard UI to display the exact JSON configuration path for each field in help dialogs.

```csharp
JsonPathInfo? GetPathInfo(string fieldKey)
```

Returns `null` for concept-only help keys (`Concept_Environment`, `Concept_TargetGroup`, `Concept_Target`) that have no corresponding JSON path. All other registered field keys return a `JsonPathInfo` with `ConfigPath` (relative to the `RayMigrator` root section), and optionally `InheritedByPaths` (paths that inherit from this field) or `InheritedFromPath` (the parent path this field inherits from).

Example entries:
- `"Repository_Timeout"` → `ConfigPath = "Repository.DbCommandTimeoutInSeconds"`
- `"ProductDefaults_MigrationErrorAction"` → `ConfigPath = "ProductDefaults.MigrationErrorAction"`, `InheritedByPaths = ["Products[].MigrationErrorAction"]`
- `"TargetGroup_TargetMigrationOrder"` → `ConfigPath = "Products[].TargetGroups[].TargetMigrationOrder"`, `InheritedFromPath = "ProductDefaults.TargetGroupDefaults.TargetMigrationOrder"`

## EnvFileGenerator (Core)

`EnvFileGenerator` (static, Core) generates an `example.env` file listing all `{ENV:}` variables referenced in the configuration.

```csharp
// Generate from a ConfigurationModel (scans Repository, DatabaseLogging, Products)
string Generate(ConfigurationModel model, Func<string, string?>? envVarResolver = null)

// Generate from exported JSON strings (scans ALL exported files for {ENV:} variables)
string GenerateFromExportedJsons(IEnumerable<KeyValuePair<string, string>> exportedJsons,
    Func<string, string?>? envVarResolver = null)
```

The optional `envVarResolver` substitutes ENV variable current values without calling `Environment.GetEnvironmentVariable` directly (required in Blazor WASM where that call is not available). When `envVarResolver` is `null`, it defaults to `Environment.GetEnvironmentVariable`.

`Generate` scans connection strings in Repository, DatabaseLogging, and all Target connection strings, as well as `MigrationFilesRootDirectory` values in each Product. For each variable found, it records where it is used (e.g. `Products[BookStore].Backend.Main.ConnectionString`).

`GenerateFromExportedJsons` is used by `ZipExportService.ExportAsync` to scan all exported JSON files (not just the base model) for `{ENV:}` references. This ensures that environment variables defined only in env or product-environment files are included in the output. Each entry records which file(s) reference the variable. Currently set environment variable values are included in the output file if present.

## EnvironmentSkeletonGenerator (Core)

`EnvironmentSkeletonGenerator` (static, Core) generates a minimal `appsettings.{env}.json` skeleton containing only connection string overrides as `{ENV:}` placeholders.

```csharp
string Generate(ConfigurationModel model, bool indented = true)
```

If a connection string already contains an `{ENV:}` placeholder, it is kept as-is. Otherwise, a generated placeholder name is used. The repository uses `{ENV:REPO_CONNECTION_STRING}`, database logging uses `{ENV:DBLOG_CONNECTION_STRING}`, and targets use `{ENV:{PRODUCT}_{TARGETGROUP}_{TARGET}_CONNECTION_STRING}` (all parts uppercased; spaces, hyphens, and dots replaced with underscores).

## CliToolPresetProvider (Core)

`CliToolPresetProvider` (`Raycoon.RayMigrator.ConfigWizard.Core.Services`, static) provides 10 hardcoded `CliToolPreset` entries covering all 5 database engines in both native and Docker variants.

```csharp
IReadOnlyList<CliToolPreset> GetAllPresets()
IReadOnlyList<CliToolPreset> GetPresetsForDatabaseType(string databaseType)
IReadOnlyList<CliToolPreset> GetDockerPresets()
CliToolPreset? GetPresetByAlias(string alias)
```

`CliToolPreset` (in `Raycoon.RayMigrator.ConfigWizard.Core.Models`) has: `Alias`, `DatabaseType`, `ExecutablePath`, `ArgumentTemplate`, `InputMode`, `SuccessExitCodes` (`List<string>`, default `["0"]`), `CliToolTimeoutInSeconds`, `Description`, `IsDockerVariant`, and `ExpectedParameterKeys` (a list of `CliToolParameters` keys the preset expects each Target to provide, used to auto-scaffold target parameters — e.g. `["Server", "User", "Password", "Database"]`).

Available aliases: `sqlcmd`, `psql`, `mariadb`, `mysql`, `sqlite3` (native); `sqlcmd-docker`, `psql-docker`, `mariadb-docker`, `mysql-docker`, `sqlite3-docker` (Docker variants).

Used by `ConfigurationScaffolder.Scaffold` to populate `CliTools` in the generated `WizardState`.

## ConfigurationScaffolder (Core)

`ConfigurationScaffolder` (`Raycoon.RayMigrator.ConfigWizard.Core.Services`) creates pre-filled `WizardState` and `ConfigurationModel` instances for the Blazor Web wizard.

```csharp
WizardState Scaffold(WizardSetupAnswers answers)
WizardState ScaffoldMinimal()
ConfigurationModel ScaffoldCombination(string productAlias, string environmentName, ConfigurationModel? baseModel = null)
```

`ScaffoldMinimal()` creates a minimal `WizardState` with a single product (`MyApp`), a single environment (`Development`), a single target group (`Backend`/SqlServer), and a single target (`BackendDB`). Called when the user clicks "Create New" on the Welcome page.

`Scaffold(answers)` creates a full `WizardState` from `WizardSetupAnswers`. It produces:
- `Repository` with `ConnectionString = "{ENV:REPO_CONNECTION_STRING}"` and `SchemaName = "ray"` (empty string for Sqlite)
- `DatabaseLogging` (if `UseDatabaseLogging`) with `DatabaseType` matching the repository type, `ConnectionString = "{ENV:DBLOG_CONNECTION_STRING}"`, and `SchemaName = "ray"` (empty string for Sqlite)
- `ProductDefaults` with sensible defaults
- `Products` with `MigrationFilesRootDirectory = "./Migrations/{Alias}"` and auto-generated ENV-based connection strings
- `CliTools` populated from `CliToolPresetProvider` native presets (if `UseCliTools`)
- `EnvironmentModels` and `ProductEnvironmentModels` in `WizardState` with per-environment connection string overrides

`ScaffoldCombination(productAlias, environmentName, baseModel?)` creates a stand-alone `ConfigurationModel` with `FileRole = ProductEnvironment`, pre-filled with sensible defaults (Repository, DatabaseLogging, ProductDefaults, Serilog, and one product with one target group and one target). When an optional `baseModel` is provided, the scaffold inherits Repository DatabaseType and SchemaName, DatabaseLogging settings, ProductDefaults, Serilog `MinimumLevelDefault`, and the product structure (TargetGroups and Targets) from the matching base product. Connection strings always become environment-specific `{ENV:..._CONNECTION_STRING_{ENV}}` placeholders. Called by `WizardStateService.StartDetailedConfiguration` (and `AddEnvironment`) when a combination is visited for the first time in the hub-and-spoke flow.

`WizardSetupAnswers` captures: `RepositoryDatabaseType`, `List<ProductSetup> Products` (each with `Alias`, `List<string> Environments`, and `List<TargetGroupSetup> TargetGroups`), `UseDatabaseLogging`, and `UseCliTools`. Each `TargetGroupSetup` has `Alias`, `DatabaseType`, and `List<string> TargetAliases`.

`WizardState` holds the complete multi-file state: `BaseModel`, `EnvironmentModels` (keyed by environment name), `ProductModels` (keyed by product alias), `ProductEnvironmentModels` (keyed by `"{Product}.{Env}"`), `CombinationEntries` (keyed by `"{Product}.{Env}"`), and `SetupAnswers`.

## DefaultsPromoter (Core)

`DefaultsPromoter` (`Raycoon.RayMigrator.ConfigWizard.Core.Services`) analyzes configuration models and promotes identical values to higher-level defaults, reducing redundancy. Returns lists of `PromotionResult` objects describing each promotion applied.

```csharp
List<PromotionResult> Promote(ConfigurationModel model)
List<PromotionResult> PromoteAcrossModels(WizardState state)
```

**`Promote(model)`** — intra-model promotion: analyzes all products within a single `ConfigurationModel`. If every product overrides the same value for a property (e.g. all products set `MigrationErrorAction = "Rollback"`), that override is promoted to `ProductDefaults.MigrationErrorAction` and the per-product overrides are cleared. The same logic applies for `TargetGroup`-level and `Target`-level overrides. Product-level properties promoted include: `MigrationErrorAction`, `RollbackErrorAction`, `MigrationFilesExtension`, `MigrationRollbackFilesPreExtension`, `MigrationFilesEncoding`, `UseCliToolAlias`, `RequireRollbackFile`, and `StopRollbackOnMissingRollbackFile`. TargetGroup-level properties promoted include: `TargetMigrationOrder`, `HashValidationScope`, and `StopRollbackOnMissingRollbackFile` (promoted via `TryPromoteTargetGroupBoolOverride` to `TargetGroupDefaults.StopRollbackOnMissingRollbackFile`). TargetGroup-level `UseCliToolAlias` overrides are cleared when identical across all target groups, but since `TargetGroupDefaults` has no `UseCliToolAlias` field the value is not written upward (clear-only pass). Target-level properties promoted include: `DbCommandTimeoutInSeconds`, `DbCommandMaxRetries`, and `DbCommandWaitTimeInMsBeforeRetry` (all promoted to `TargetDefaults`).

**`PromoteAcrossModels(state)`** — cross-model promotion: analyzes all `ProductEnvironmentModels` in a `WizardState` and promotes common values upward:
- Values identical across all combinations are promoted to `BaseModel`
- Values identical across all combinations of one environment are promoted to `EnvironmentModels[env]`

Fields promoted across all models: `Repository.DatabaseType`, `Repository.SchemaName`, `Repository.ConnectionString`, `ProductDefaults.MigrationErrorAction`, `ProductDefaults.RollbackErrorAction`, `ProductDefaults.MigrationFilesExtension`, `ProductDefaults.MigrationFilesEncoding`, `ProductDefaults.RequireRollbackFile`, `ProductDefaults.StopRollbackOnMissingRollbackFile`, and `Serilog.MinimumLevelDefault`. Per-environment promotion covers `Repository.ConnectionString` within each environment group.

After cross-model field promotion, `PromoteAcrossModels` also calls `ReconcileBaseProducts`, which synchronizes `BaseModel.Products` with the current structure in `ProductEnvironmentModels`. It adds TargetGroups and Targets that appear in any PE model but are missing from the base, removes stale TargetGroups and Targets that no longer appear in any PE model, and copies the common `MigrationFilesRootDirectory` when all PE models agree on the same value.

The Overview page calls both methods automatically in `OnInitialized`: cross-model promotion first, then intra-model promotion on the base model.

`PromotionResult` has `PropertyName`, `PromotedValue`, `AffectedProducts` (count), and `Level` (`"ProductDefaults"`, `"TargetGroupDefaults"`, or `"BaseModel"`).

## ConfigurationFileParser (Core)

`ConfigurationFileParser` (static, Core) parses a set of named JSON strings (filename → content) into a `WizardState`. Used by the Blazor Web wizard's file-import flow. It is IO-free and works with in-memory content.

```csharp
static WizardState Parse(Dictionary<string, string> files)

// Classify a single filename into its hierarchy role, product, and environment
static (ConfigFileRole role, string? product, string? environment) ClassifyFileName(string fileName)
```

File classification follows the `appsettings` naming convention described in [File Hierarchy](./file-hierarchy.md#file-family-discovery). Unknown filenames are skipped. Parse errors on individual files are silently skipped.

After parsing, `Parse` reverse-engineers `WizardSetupAnswers` from the loaded models, creates one `ProductEnvironmentEntry` (with `WizardCompleted = false`) per imported PE model, and merges parent layers (Base → Environment → Product → PE) into each imported PE model using `ConfigFileMerger.MergeChain` on their preserved original JSON. This ensures the stepper shows effective (inherited) values when a user walks through an imported combination. Scaffolded PE models that have no `PreservedDocument` are skipped by the merge step.

## WizardCliToolParameterResolver (Core)

`WizardCliToolParameterResolver` (`Raycoon.RayMigrator.ConfigWizard.Core.Services`, static) resolves the list of parameter keys expected by a CLI tool, with fallback to `CliToolPresetProvider` for unknown aliases. Placeholder extraction itself lives in `CliToolPlaceholderExtractor` in the shared `Raycoon.RayMigrator.Validation.Helpers` library.

```csharp
// Resolve parameter keys for a given CLI tool alias — reads placeholders from the matching
// CliToolModel's ArgumentTemplate via CliToolPlaceholderExtractor.ExtractParameterKeys; falls
// back to CliToolPreset.ExpectedParameterKeys when the alias matches a preset but the template
// has no parsed placeholders.
static List<string> ResolveParameterKeys(string? cliToolAlias, IReadOnlyList<CliToolModel> cliTools)
```

`CliToolPlaceholderExtractor.ExtractParameterKeys(string?)` (in `Raycoon.RayMigrator.Validation.Helpers`) uses the regex `\{(\w+)\}` to extract user-editable placeholders. `FilePath` is reserved (`CliToolPlaceholderExtractor.ReservedKeys`) and excluded from the result because it is resolved by the runtime, not by user-supplied parameters. A companion `ExtractAllPlaceholders` method returns every placeholder including reserved ones.

## WizardStepId (Web)

`WizardStepId` is an enum defined in `WizardStateService.cs` that assigns a fixed identity to each of the 6 stepper steps, independent of display order or Expert Mode visibility. Used by `WizardStateService` to track `CurrentStepId` and the set of visited steps per product+environment combination.

| Value | Numeric | Step |
|-------|---------|------|
| `Repository` | 0 | Repository configuration step |
| `DatabaseLogging` | 1 | Database Logging step |
| `CliTools` | 2 | CLI Tools step (Expert Mode only, also shown if the combination already has CLI tools) |
| `ProductDefaults` | 3 | Product Defaults step |
| `ProductSettings` | 4 | Product Settings step |
| `Serilog` | 5 | Serilog step |

`WizardHost.razor` maps between `WizardStepId` and the MudStepper display index (which omits hidden steps): when CLI Tools is hidden, `ProductDefaults` and later steps shift down by one display index, but the underlying `WizardStepId` values remain stable.

## WizardStateService (Web)

`WizardStateService` (`Raycoon.RayMigrator.ConfigWizard.Web.Services`) is the central state manager for the Blazor Web wizard. It has `Scoped` DI lifetime (one instance per browser tab) and exposes both the Core `WizardState` and UI-level navigation state.

### State Properties

| Property | Type | Description |
|----------|------|-------------|
| `State` | `WizardState` | Core state (all configuration models and combination entries) |
| `Answers` | `WizardSetupAnswers` | Product/environment structure used by the Hub page |
| `CurrentPhase` | `WizardPhase` | Active top-level phase (`Start` / `Hub` / `GuidedConfig` / `Overview`) |
| `CurrentStepId` | `WizardStepId` | Active step identity within the stepper (fixed ID, independent of display index and Expert Mode visibility) |
| `LastValidationResult` | `WizardValidationResult?` | Cached result from the last `ValidateAll()` call |
| `IsImported` | `bool` | True when an existing configuration was uploaded on the Welcome page |
| `IsExpertMode` | `bool` | Expert Mode toggle (shows all override fields in step sections) |
| `SelectedProductAlias` | `string?` | Product currently being configured in Detailed Configuration |
| `SelectedEnvironmentName` | `string?` | Environment currently being configured in Detailed Configuration |
| `StateChanged` | `event Action?` | Fires when state changes and components should re-render |
| `BaseModel` | `ConfigurationModel` | Shortcut to `State.BaseModel` |
| `EnvironmentKeys` | `IReadOnlyCollection<string>` | All environment model keys currently in state |

### Phase Transition Methods

| Method | Description |
|--------|-------------|
| `StartNewConfiguration()` | Scaffolds a minimal state via `ScaffoldMinimal()` and transitions to Hub (`CurrentStepId` is reset to `Repository`) |
| `GoToHub()` | Navigates to the Hub phase |
| `GoToOverview()` | Navigates to the Overview phase |
| `GoToGuidedConfig()` | Navigates to the GuidedConfig phase and resets `CurrentStepId` to `Repository` |
| `GoToStart()` | Returns to the Welcome phase, resets `IsExpertMode` to false, and clears all visited-step tracking |
| `StartDetailedConfiguration(product, env)` | Scaffolds a combination model if needed (or if the existing one is empty), ensures a `CombinationEntry`, sets selection, and transitions to GuidedConfig |
| `CompleteDetailedConfiguration()` | Marks the current combination as `WizardCompleted = true` and returns to Hub |
| `ImportFiles(files)` | Parses uploaded JSON files via `ConfigurationFileParser`, sets `IsImported = true`, adopts the reverse-engineered `Answers`, clears the selection and validation cache, and notifies state change (the caller handles the phase transition) |
| `SyncStructure(allEnvironments)` | Regenerates environment and product-environment models from a list of all environment names, preserving existing models where keys still match. Typically called after structural edits to products/environments |
| `Reset()` | Resets all state to defaults and returns to the Welcome phase |

### Product/Environment CRUD Methods

| Method | Description |
|--------|-------------|
| `AddProduct(alias)` | Adds a product to `BaseModel.Products` and `Answers.Products` with a default `Backend`/`MainDB` TargetGroup/Target scaffold and an ENV-based connection string |
| `RemoveProduct(alias)` | Removes a product from `BaseModel.Products` and `Answers.Products`, cascades to all its product-environment models, combination entries, and any `ProductModels` entry, then prunes orphaned environment models |
| `RenameProduct(old, new)` | Renames a product alias in `BaseModel.Products`, `Answers.Products`, and all PE/combination/product models (re-keying dictionaries and updating the inner product alias and logical file paths) |
| `AddEnvironment(product, env)` | Adds an environment to a product's `Answers` entry, scaffolds a PE model via `ConfigurationScaffolder.ScaffoldCombination`, creates a `ProductEnvironmentEntry`, and ensures an env-level skeleton `ConfigurationModel` exists |
| `RemoveEnvironment(product, env)` | Removes an environment from a product's `Answers` entry and its PE model and combination entry, then prunes orphaned environment models |
| `RenameEnvironment(product, old, new)` | Renames an environment for a specific product and re-keys all affected PE models, combination entries, and (when the old env is no longer referenced by any product) the env-level model |

### Validation and Promotion

| Method | Description |
|--------|-------------|
| `ValidateAll()` | Validates `BaseModel` and every populated product-environment model (skips empty PE shells with no Products), prefixes PE issue paths with `[{Product}.{Env}] `, aggregates results, caches in `LastValidationResult`, and notifies state change |
| `ValidateSection(name)` | Validates a single section of `BaseModel`. Supported names: `Repository`, `DatabaseLogging` (no-op when `BaseModel.DatabaseLogging` is null), `ProductDefaults`, `CliTools` |
| `ValidateCombination(product, env)` | Validates a specific combination's `ProductEnvironmentModel` (returns an empty result if the key does not exist) |
| `PromoteDefaults()` | Runs `DefaultsPromoter.PromoteAcrossModels` (cross-model) then `DefaultsPromoter.Promote` (intra-model on `BaseModel`). Sets `BaseModel.IsModified = true` when any promotion was applied |

### Step Tracking

`GetVisitedSteps()` returns a `HashSet<WizardStepId>` of all steps that have been visited for the currently selected product+environment combination. The returned set always contains at least `WizardStepId.Repository`. The stepper uses this to show visited-step indicators. Visited steps are tracked per combination, keyed by `"{Product}.{Env}"`, and are cleared when `GoToStart()` is called.

### Combination Model Access

`GetSelectedCombinationModel()` returns the `ConfigurationModel` for the currently selected `SelectedProductAlias`/`SelectedEnvironmentName` combination from `State.ProductEnvironmentModels`. The `WizardHost` component (Detailed Configuration stepper) binds all 6 section components to this model rather than to `BaseModel`.

`GetEnvironmentModel(env)` returns a specific environment model by name, or `null` if not present.

`GetProductEnvironmentModel(key)` returns a specific product-environment model by key (format `"{Product}.{Env}"`), or `null` if not present.

`GetExportJsons()` returns a cached `Dictionary<string, string>` of all pruned export JSON strings keyed by filename (e.g. `"appsettings.json"`, `"appsettings.Development.json"`). Delegates to `ZipExportService.ComputeExportJsons` and caches the result until the next state change. `GetBaseJson()`, `GetEnvironmentJson(env)`, and `GetProductEnvironmentJson(key)` are convenience wrappers that look up the appropriate entry in this cache.

`NotifyStateChanged()` fires the `StateChanged` event, causing subscribed components to re-render. Also invalidates the export JSON cache. Called internally by most mutation methods and also available for external components that modify state properties directly.

The Hub page builds a `CombinationInfo` list from `Answers.Products` and reads `WizardCompleted` from `State.CombinationEntries` to display TODO/DONE status.

## Web-Specific Infrastructure Services (Web)

These services handle Blazor WASM infrastructure concerns and are registered in the Web project's DI container alongside `WizardStateService`.

**`ZipExportService`** (`Raycoon.RayMigrator.ConfigWizard.Web.Services`) builds an in-memory ZIP archive containing all configuration files derived from a `WizardState`. Called by the Overview page's Export button. Requires a `FileInteropService` injected via its constructor.

```csharp
// Instance method — triggers ZIP download via FileInteropService
Task ExportAsync(WizardState state)

// Static method — computes all pruned export JSON strings keyed by filename;
// used by both ExportAsync and WizardStateService.GetExportJsons() for JSON preview
static Dictionary<string, string> ComputeExportJsons(WizardState state)
```

The export applies a full hierarchy-pruning pass: base-file properties that are overridden in every runtime combination are removed from the base file; environment and product-file properties that are already covered by their respective product-environment files are pruned as well. This keeps each file minimal and avoids redundant repetition across the hierarchy.

`ExportAsync` also calls `EnvFileGenerator.GenerateFromExportedJsons` (scanning all exported JSON files) to produce an `example.env` file and includes it in the ZIP alongside the configuration files. The ZIP is downloaded as `raymigrator-config.zip`.

**`FileInteropService`** (`Raycoon.RayMigrator.ConfigWizard.Web.Services`) wraps the browser's file-download JS interop (`downloadFileFromBytes`). Used by `ZipExportService` to trigger a browser download.

```csharp
Task DownloadFileAsync(string fileName, string contentType, byte[] content)
```

**`JsonHighlightService`** (`Raycoon.RayMigrator.ConfigWizard.Web.Services`) applies inline-style syntax highlighting to a JSON string (HTML-encodes first, then wraps keys, string values, literals, and numbers with `<span style="color:#…">` tags) and returns a `MarkupString` for direct Blazor rendering. Used in the Overview page's JSON preview panel.

```csharp
MarkupString Highlight(string json)
```

**`LocalizationService`** (`Raycoon.RayMigrator.ConfigWizard.Web.Services`) manages UI language selection (English/German). It wraps Core's `ContextHelpProvider` to provide localized section and field help and persists the selected language in browser `localStorage`. Supported languages: `"en"` (English) and `"de"` (Deutsch).

**`WizardMudLocalizer`** (`Raycoon.RayMigrator.ConfigWizard.Web.Services`) is a custom `MudLocalizer` subclass that translates MudBlazor component strings (e.g. Stepper Previous/Next/Complete/Skip/Reset buttons) using the wizard's `LocalizationService`. It maps MudBlazor localization keys like `MudStepper_Previous`, `MudStepper_Next`, `MudStepper_Complete`, `MudStepper_Skip`, `MudStepper_Reset` to the corresponding wizard translation keys (`Common.Back`, `Common.Next`, `Phase2.GoToOverview`, `Summary.Restart`, etc.). Registered via `AddTransient<MudLocalizer, WizardMudLocalizer>()` in `Program.cs`.

## Related Documentation

- [Architecture](./architecture.md)
- [File Hierarchy](./file-hierarchy.md)
- [Validation Reference](./validation.md)
- [Unit Tests](../10-testing/unit-tests.md#configwizardcore-unit-tests)
