# Unit Tests

Guide for running and writing unit tests for RayMigrator.

## Overview

Unit tests verify RayMigrator's internal logic without requiring database connections or Docker containers. They test:

- SQL parsing (TOML metadata, SQL block splitting, line endings)
- File classification and filtering
- Configuration validation and merging (ProductDefaults, MigSettings inheritance, RayAttributes)
- Migration ordering and execution logic
- Error handling behavior (MigrationErrorAction, RollbackErrorAction)
- Hash computation and validation
- Environment variable replacement
- Sensitive data masking
- String extensions and utility functions
- Template cache configuration validation
- Schema name validation
- Retry logic (including custom predicate overloads)
- DAL factory discovery and caching, DAL type mapping, engine-specific parameter handling, DAL base transient error detection, and DAL `CreateConnection` ADO.NET type verification
- SQLite foreign-key enforcement wiring (`DalSqlite.EnsureForeignKeysEnabled` connection-string transform)
- Atomic shared-connection guard (`CanUseSharedConnection` — four required conditions: `UseTransaction=true`, `MigrationErrorAction != Ignore`, matching `DatabaseType`, identical `ConnectionString`; `DbCommandMaxRetries` is explicitly not a condition)
- CLI tool configuration (CliToolOptions, InputModeEnum parsing)
- CLI tool validation (UseCliToolAlias reference checks, duplicate alias detection)
- CLI tool execution helpers (ResolveUseCliToolAlias, ResolveCliToolArguments)
- CLI tool process execution (CliToolExecutor: File and Stdin modes, exit code evaluation, timeout, cancellation)
- UseCliToolAlias inheritance cascade (ProductDefaults -> Product -> TargetGroup -> Target)
- Model and exception correctness
- SQL block splitting bypass for CLI tool execution
- Hash validation scope resolution (target group lookup, fallback behavior)
- Migration command exhaustiveness guards
- `--config-dir` CLI option and `JsonOptionsSource` configDir parameter validation and file resolution
- DAL-level type conventions and template casing discipline (PostgreSQL TIMESTAMPTZ/TEXT, MySQL/MariaDB TIMESTAMP/CURRENT_TIMESTAMP, SQLite no-AUTOINCREMENT, snake_case identifier guards for PG and MySQL/MariaDB)
- EnvironmentId FK feature (template structure, parameter binding, logging propagation)
- `MigrationContext.Clone` regression coverage (`EnvironmentId`, `MigratorMetaId`, `MigrationEvent` propagation)
- `RepositoryQueryHelper.ToSnakeCase` mapping (DAL-017 brand-token preservation)

The project contains approximately **1196 unit test cases** organized into **114 test classes** across **77 source test files** using a **P0-P3 priority system** (each file may contain multiple related test classes):

- **P0** — Critical parsing and core behavior (9 files, 15 classes, 154 tests)
- **P1** — Business logic and configuration (53 files, 71 classes, 851 tests)
- **P2** — Secondary features and utilities (13 files, 21 classes, 149 tests)
- **P3** — Models and exceptions (2 files, 6 classes, 41 tests)

Additionally, there is 1 test class outside the priority system (`MigrationCommandExhaustivenessTests`) that ensures every `MigrationCommand` enum value is handled in the engine pipeline. The `Helpers/` directory contains 2 non-test files (`TestFactories.cs`, `CapturingLogger.cs`).

## ConfigWizard.Core Unit Tests

A separate **ConfigWizard.Core unit test project** (`Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core`) contains approximately **419 tests** across **28 test classes** in **28 files**, covering the zero-dependency shared domain library:

| Test File | Description |
|-----------|-------------|
| `CliToolInheritanceAndSerializerTests.cs` | CLI tool inheritance walking via `WizardValidationInputAdapter` and serializer skip-rules when no effective alias is set |
| `CliToolModelValidationTests.cs` | CLI tool model validation |
| `CliToolPresetProviderTests.cs` | CLI tool preset catalog |
| `ConfigFileMergerTests.cs` / `ConfigFileMergerAdditionalTests.cs` | JSON merge semantics |
| `ConfigurationFileParserTests.cs` / `ConfigurationFileParserAdditionalTests.cs` | Config file role/pattern parsing |
| `ConfigurationScaffolderTests.cs` | Migration directory scaffold generation |
| `ConfigurationSerializerTests.cs` / `ConfigurationSerializerAdditionalTests.cs` | JSON load/save round-trips |
| `ConfigurationValidatorTests.cs` / `ConfigurationValidatorProductTests.cs` / `ConfigurationValidatorCrossFieldTests.cs` | Validation rules including cross-field structural rules (duplicate aliases, semantic contradictions, CLI tool constraints, default cascade completeness) |
| `ContextHelpProviderTests.cs` | Context-sensitive help text |
| `DefaultsPromoterTests.cs` / `DefaultsPromoterAdditionalTests.cs` / `DefaultsPromoterAcrossModelsTests.cs` | Defaults promotion logic, including cross-model promotion via `PromoteAcrossModels` |
| `JsonPathRegistryTests.cs` | `JsonPathRegistry` path info lookups (config path, inheritance relationships) |
| `PromotionEndToEndTests.cs` | End-to-end scenarios verifying the interaction between `DefaultsPromoter` (promotion) and diff-based serialization (`ConfigurationSerializer.ToJson(model, baseModel)`) across all hierarchy levels, including `StopRollbackOnMissingRollbackFile` at both product and target-group level |
| `EnvFileGeneratorTests.cs` / `EnvFileGeneratorAdditionalTests.cs` | example.env generation |
| `EnvironmentSkeletonGeneratorTests.cs` / `EnvironmentSkeletonGeneratorAdditionalTests.cs` | Environment skeleton files |
| `InheritanceResolverTests.cs` / `InheritanceResolverAdditionalTests.cs` | Effective value resolution |
| `OverridableValueTests.cs` | OverridableValue&lt;T&gt; behavior |
| `WizardSetupAnswersTests.cs` | Wizard setup answer model |
| `WizardValidationResultTests.cs` | Validation result aggregation |

Target framework: `net10.0` (single target). No Docker or database connection required.

## ConfigWizard.Web Unit Tests

A separate **ConfigWizard.Web unit test project** (`Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web`) contains approximately **248 tests** across **7 test classes** covering service classes of the Blazor WASM standalone configuration wizard:

| Test File | Class | Tests | Description |
|-----------|-------|-------|-------------|
| `EndToEndCliToolValidationTests.cs` | `EndToEndCliToolValidationTests` | ~2 | End-to-end import-three-files scenario verifying RULE_3_8 validation surfaces |
| `JsonHighlightServiceTests.cs` | `JsonHighlightServiceTests` | ~20 | JSON syntax highlighting service |
| `LocalizationServiceTests.cs` | `LocalizationServiceTests` | ~24 | Localization service (DE/EN string resolution) |
| `WizardHostStepIndexTests.cs` | `WizardHostStepIndexTests` | ~18 | Step index adjustment and `StepperKey` logic in `WizardHost.razor` (CLI Tools step visibility, `OnExpertModeChanged` shift logic) |
| `WizardStateServiceTests.cs` | `WizardStateServiceTests` | ~124 | Central wizard state management (phases, navigation, reset) |
| `WizardStateServiceValidateAllPeModelsTests.cs` | `WizardStateServiceValidateAllPeModelsTests` | ~6 | `WizardStateService.ValidateAll()` aggregation across BaseModel and merged ProductEnvironmentModels |
| `ZipExportServiceTests.cs` | `ZipExportServiceTests` | ~54 | ZIP archive generation from wizard state (JS interop faked) |

Target framework: `net10.0` (single target). No Docker or database connection required. Tests are plain xUnit tests against service classes (no Blazor component rendering via bUnit).

## Validation Rule-Catalog Unit Tests

A separate **Validation unit test project** (`Raycoon.RayMigrator.Tests.Unit.Validation`) contains approximately **69 tests** that exercise the standalone rule catalog in `Raycoon.RayMigrator.Validation`. The project intentionally references **only** the `Validation` project (no Core, no ConfigWizard.Core), enforcing the rule that the catalog stays WASM-safe and zero-dependency.

| Folder / File | Description |
|---------------|-------------|
| `RuleCatalogTests.cs` | Catalog-level guarantees (rule registration, ID uniqueness) |
| `Rules/AliasUniquenessRuleTests.cs` | Duplicate alias detection across products / target groups / targets |
| `Rules/CliToolDefinitionsRuleTests.cs` | CLI tool definition validation (alias presence, executable path, etc.) |
| `Rules/CliToolParametersRuleTests.cs` | CLI tool parameter rules (placeholder/value pairs) |
| `Rules/CliToolReferencesRuleTests.cs` | Cross-references between `UseCliToolAlias` and `CliTools[]` |
| `Rules/ConnectionStringRuleTests.cs` | Connection string presence and shape |
| `Rules/DefaultCascadeRuleTests.cs` | Default cascade completeness |
| `Rules/SchemaRuleTests.cs` | Repository / DatabaseLogging schema constraints |
| `Rules/SemanticContradictionsRuleTests.cs` | Semantic contradictions (incompatible option combinations) |
| `Rules/TargetGroupMigrationOrderRuleTests.cs` | `TargetGroupMigrationOrder` configuration validation |
| `Helpers/CliToolPlaceholderExtractorTests.cs` | Placeholder extraction helper used by CLI tool rules |
| `Helpers/ExitCodeExpressionValidatorTests.cs` | Exit-code expression parsing/validation |
| `Helpers/InputFactory.cs` | Test input factory (non-test helper) |

Target framework: `net10.0` (single target). No Docker or database connection required.

## Prerequisites

1. .NET 10+ SDK installed (test project targets net10.0)
2. No Docker or database connection required

## Running Tests

### All Unit Tests

```bash
# Main unit tests
dotnet test Raycoon.RayMigrator.Tests.Unit/

# ConfigWizard.Core unit tests
dotnet test Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core/

# ConfigWizard.Web unit tests
dotnet test Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web/

# Validation rule-catalog unit tests
dotnet test Raycoon.RayMigrator.Tests.Unit.Validation/
```

### Specific Test Class

```bash
dotnet test Raycoon.RayMigrator.Tests.Unit/ --filter "FullyQualifiedName~ExtractTomlAndSqlTests"
```

> **Note**: Test class names do not carry the P0-P3 prefix (the prefix is only in the file names). Use the actual class name when filtering (e.g. `ExtractTomlAndSqlTests`, `FilterAlreadyMigratedFilesTests`).

## Project Structure

```
Raycoon.RayMigrator.Tests.Unit/
├── Helpers/
│   ├── TestFactories.cs          # Shared factory methods for test objects
│   └── CapturingLogger.cs        # ILogger<T> that captures log entries
├── MigrationCommandExhaustivenessTests.cs
├── P0_CliToolConfigTests.cs
├── P0_ConfigDirTests.cs
├── P0_EnvironmentVariableReplacerTests.cs
├── P0_FileClassificationTests.cs
├── P0_LineEndingTests.cs
├── P0_MigrationRunModeExtensionsTests.cs
├── P0_ShouldSkipBlockSplittingTests.cs
├── P0_SplitSqlIntoBlocksTests.cs
├── P0_TomlParsingTests.cs
├── P1_AutoFixOrphanedRunTests.cs
├── P1_BuildMigrationRunSettingsJsonTests.cs
├── P1_CanUseSharedConnectionTests.cs
├── P1_CliToolExecutionHelpersTests.cs
├── P1_CliToolExecutorTests.cs
├── P1_CliToolValidationTests.cs
├── P1_DalBaseTypeAndParameterTests.cs
├── P1_DalCreateConnectionTests.cs
├── P1_DalFactoryTests.cs
├── P1_DalIsTransientTests.cs
├── P1_DalParameterEscapingTests.cs
├── P1_DalSqliteForeignKeysTests.cs
├── P1_DalSqliteParameterTests.cs
├── P1_DalSqlServerOverrideTests.cs
├── P1_EnvironmentCheckInsertTests.cs
├── P1_EnvironmentIdFkTests.cs
├── P1_ExitCodeMatcherTests.cs
├── P1_FilterAlreadyMigratedFilesTests.cs
├── P1_FilterByTargetGroupTests.cs
├── P1_FilterByTargetReleaseTests.cs
├── P1_FlatLayoutAmbiguityTests.cs
├── P1_HandleMigrationErrorBehaviorTests.cs
├── P1_MigrationContextCloneTests.cs
├── P1_MigrationErrorActionIgnoreTests.cs
├── P1_MigrationErrorActionInheritanceTests.cs
├── P1_MigrationSafetyWarningTests.cs
├── P1_MigSettingsInheritanceTests.cs
├── P1_MySqlMariaDbTypeConventionsTests.cs
├── P1_OutOfOrderDetectionTests.cs
├── P1_PostgreSqlTypeConventionsTests.cs
├── P1_ProductCheckInsertTests.cs
├── P1_ProductDefaultsPostConfigureOptionsTests.cs
├── P1_RayAttributeTests.cs
├── P1_RayMigratorOptionsValidatorTests.cs
├── P1_RepositoryQueryHelperToSnakeCaseTests.cs
├── P1_RequireRollbackFileValidationTests.cs
├── P1_ResolveHashValidationScopeTests.cs
├── P1_ResumeFromBlockTests.cs
├── P1_RetryHelperCustomPredicateTests.cs
├── P1_RetryHelperTests.cs
├── P1_RollbackErrorActionTests.cs
├── P1_SchemaNameValidationTests.cs
├── P1_SensitiveDataMaskerTests.cs
├── P1_Sha256Tests.cs
├── P1_SqlBlockEnvironmentVariableTests.cs
├── P1_SqliteTypeConventionsTests.cs
├── P1_SqlTemplateStructureTests.cs
├── P1_TargetGroupMigrationOrderTests.cs
├── P1_TargetMigrationOrderExecutionTests.cs
├── P1_TemplateCacheCfgValidationTests.cs
├── P1_TemplateExecutorEnvironmentIdTests.cs
├── P1_TryFinalizeCompletedMigrationTests.cs
├── P1_UseCliToolAliasInheritanceTests.cs
├── P2_EnsureConnectionStringOptionsTests.cs
├── P2_EnvironmentIdLoggingPipelineTests.cs
├── P2_FixCommandTests.cs
├── P2_MigrationAlreadyRunningTests.cs
├── P2_MigrationFileSqlLoggingTests.cs
├── P2_MigrationIdLoggingPipelineTests.cs
├── P2_MySqlMariaDbIdentifierCasingTests.cs
├── P2_OptionsEnumPropertyTests.cs
├── P2_PostgreSqlIdentifierCasingTests.cs
├── P2_RayMigratorDatabaseSinkTests.cs
├── P2_StringExtensionsTests.cs
├── P2_TemplateResultCodeTests.cs
├── P2_ToDetailStringMaskingTests.cs
├── P3_BaselineAndInfoModelTests.cs
├── P3_ModelAndExceptionTests.cs
└── GlobalUsings.cs
```

## Test Classes by Priority

### P0 — Critical Parsing and Core Behavior

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P0_CliToolConfigTests` | `CliToolOptionsInputModeTests` | `CliToolOptions.InputModeEnum` parsing (null, empty, valid, invalid, case-sensitive behavior) |
| `P0_ConfigDirTests` | `ConfigDirTests` | `--config-dir` CLI option and `JsonOptionsSource` configDir parameter: null/empty/whitespace fallback to CWD, non-existent directory validation, file resolution from custom directory, `{ENV:VAR}` resolution, `CommandLineConfiguration` parsing with `--config-dir` and `-cd` aliases across all commands |
| `P0_EnvironmentVariableReplacerTests` | `EnvironmentVariableReplacerTests` | `{ENV:VAR}` placeholder replacement in configuration values |
| `P0_FileClassificationTests` | `FileClassificationTests` | Migration file discovery, rollback file matching, environment/target filtering |
| `P0_LineEndingTests` | `LineEndingExtractTomlAndSqlTests`, `LineEndingParseTomlConfigTests`, `LineEndingSplitSqlIntoBlocksTests`, `LineEndingHashSensitivityTests` | Line ending normalization across platforms (CRLF, LF, CR) |
| `P0_MigrationRunModeExtensionsTests` | `MigrationRunModeExtensionsTests` | `MigrationRunMode` enum extension methods |
| `P0_ShouldSkipBlockSplittingTests` | `ShouldSkipBlockSplittingTests` | `MigrationService.ShouldSkipBlockSplitting` — CLI tools execute files as single units, bypassing SQL block splitting |
| `P0_SplitSqlIntoBlocksTests` | `SplitSqlIntoBlocksTests` | SQL statement splitting by engine separator (`GO` for SqlServer, `;` for others) |
| `P0_TomlParsingTests` | `ExtractTomlAndSqlTests`, `ParseTomlConfigTests`, `ParseTomlEnumTests`, `GetValidEnumValuesTests` | TOML metadata header parsing from migration file comments |

### P1 — Business Logic and Configuration

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P1_AutoFixOrphanedRunTests` | `AutoFixOrphanedRunTests` | Automatic detection and repair of orphaned MigrationRun records |
| `P1_BuildMigrationRunSettingsJsonTests` | `BuildMigrationRunSettingsJsonTests` | `MigrationRunSettingsJson` serialization for MigrationRunMeta |
| `P1_CanUseSharedConnectionTests` | `CanUseSharedConnectionTests` | `MigrationService.CanUseSharedConnection` static guard — four conditions required for atomic shared-connection path: `UseTransaction=true`, `MigrationErrorAction != Ignore` (ignoreBlockErrors=false), matching `DatabaseType` (case-insensitive), identical `ConnectionString` (ordinal). `DbCommandMaxRetries` is explicitly not a guard condition; retries are handled at file level within the atomic path. |
| `P1_CliToolExecutionHelpersTests` | `ResolveUseCliToolAliasTests`, `ResolveCliToolArgumentsTests` | `MigrationService.ResolveUseCliToolAlias` (file vs target alias precedence) and `MigrationService.ResolveCliToolArguments` (placeholder substitution in CLI argument templates) |
| `P1_CliToolExecutorTests` | `CliToolExecutorTests` | `CliToolExecutor.ExecuteAsync` with real OS processes: File mode, Stdin mode, exit code evaluation (custom success/error codes, unexpected codes), stderr capture, timeout (`CliToolTimeoutException`), nonexistent executable (`CliToolExecutionException`), cancellation, and duration measurement. Platform: macOS and Linux only (skipped on Windows). |
| `P1_CliToolValidationTests` | `CliToolValidationTests` | `RayMigratorOptionsValidator` CLI tool validation (duplicate aliases, UseCliToolAlias referencing non-existent tools, case-insensitive alias matching) |
| `P1_DalBaseTypeAndParameterTests` | `DalBaseTypeAndParameterTests` | `DalBase` type mapping (`TryGetDbTypeForType`), `TryGetDbSpecificSqlParameter`, `DalParameterList`, and `DalParameter` construction and formatting |
| `P1_DalCreateConnectionTests` | `DalCreateConnectionTests` | `DAL.CreateConnection` — each DAL returns the correct concrete ADO.NET connection type in a closed/unopened state; `DalExample` stubs throw `NotImplementedException` |
| `P1_DalFactoryTests` | `DalFactoryTests` | `DalFactory` discovery, caching by connection string, error handling for unknown types, and `RegisteredDalTypes`/`ScanAssemblyForDals` |
| `P1_DalIsTransientTests` | `DalIsTransientTests` | `DalBase.IsTransient` base implementation: `TimeoutException`, non-transient exceptions (including `OperationCanceledException`), and recursive inner-exception traversal |
| `P1_DalParameterEscapingTests` | `DalParameterEscapingTests` | DAL parameter escaping for SQL injection prevention |
| `P1_DalSqliteForeignKeysTests` | `DalSqliteForeignKeysTests` | `DalSqlite.EnsureForeignKeysEnabled` connection-string transformation (DAL-001): appends `Foreign Keys=True` when missing, preserves explicit values |
| `P1_DalSqliteParameterTests` | `DalSqliteParameterTests` | SQLite-specific `FormatParameterValue` and `SubstituteParameters` (null/bool/string escaping) |
| `P1_DalSqlServerOverrideTests` | `DalSqlServerOverrideTests` | SQL Server `ConvertToDbValue` DateTime clamping (pre-1753 dates) and `CreateParameter` string size logic |
| `P1_EnvironmentCheckInsertTests` | `EnvironmentCheckInsertTests` | `Repository_Environment_CheckInsert` feature: TemplateType enum membership, MigrationEvent EventId, `MigrationState.EnvironmentId`, `TemplateResultCode.EnvironmentNameEmpty`, and SQL template structural patterns across all 5 engines (NameLower lookup, TOML header DatabaseType/TemplateType) |
| `P1_EnvironmentIdFkTests` | `EnvironmentIdFkTests` | Structural SQL-template tests for the EnvironmentId FK feature: confirms Environment text column is replaced with EnvironmentId INT FK across all 5 engines and that all INSERT/SELECT templates bind `@EnvironmentId` (not `@Environment`) |
| `P1_ExitCodeMatcherTests` | `ExitCodeMatcherTests` | `ExitCodeMatcher` parsing and evaluation for CLI tool success/error code lists and range notation (`"0"`, `"0,1"`, `"0..5"`) |
| `P1_FilterAlreadyMigratedFilesTests` | `FilterAlreadyMigratedFilesTests` | Filtering out already-migrated files from the migration plan |
| `P1_FilterByTargetGroupTests` | `FilterByTargetGroupTests` | `--target-group` CLI option filtering logic |
| `P1_FilterByTargetReleaseTests` | `FilterByTargetReleaseTests` | `--to-release` CLI option filtering logic |
| `P1_FlatLayoutAmbiguityTests` | `FlatLayoutAmbiguityTests` | Flat migration directory layout ambiguity detection (`ValidateFlatLayoutAmbiguity`) and target group alias casing validation (`ValidateTargetGroupAliasCasing`) |
| `P1_HandleMigrationErrorBehaviorTests` | `HandleMigrationErrorBehaviorTests` | Error handling dispatch based on `MigrationErrorAction` |
| `P1_MigrationContextCloneTests` | `MigrationContextCloneTests` | Regression coverage for `MigrationContext.Clone`: ensures `EnvironmentId`, `MigratorMetaId`, and `MigrationEvent` propagate to the cloned instance |
| `P1_MigrationErrorActionIgnoreTests` | `MigrationErrorActionIgnoreParsingTests`, `MigrationErrorActionIgnoreMigSettingsTests`, `MigrationErrorActionIgnoreHandleMigrationErrorTests`, `MigrationErrorActionIgnoreFullHierarchyTests` | `MigrationErrorAction.Ignore` behavior (continue after error) |
| `P1_MigrationErrorActionInheritanceTests` | `MigrationErrorActionOverrideResolutionTests`, `MigrationErrorActionTomlMigSettingsMergeTests`, `MigrationErrorActionMultiLevelMigSettingsTests`, `MigrationErrorActionFullHierarchyTests` | `MigrationErrorAction` inheritance chain (product -> release -> targetgroup -> file) |
| `P1_MigrationSafetyWarningTests` | `MigrationSafetyWarningTests` | Safety warnings for dangerous migration configurations |
| `P1_MigSettingsInheritanceTests` | `ParseMigSettingsFileTests`, `LoadMigSettingsDefaultsTests`, `ResolveMigSettingsForFileTests` | `migsettings.txt` inheritance merging (product -> release -> targetgroup) |
| `P1_MySqlMariaDbTypeConventionsTests` | `MySqlMariaDbTypeConventionsTests` | DAL-014 (`DATETIME` → `TIMESTAMP`, `UTC_TIMESTAMP()` → `CURRENT_TIMESTAMP`) and DAL-015 (explicit CHARSET/COLLATE per engine) for MySQL/MariaDB templates, plus the TOML Version / `@v_repository_version` consistency invariant |
| `P1_OutOfOrderDetectionTests` | `DetectOutOfOrderFilesTests` | Out-of-order migration detection and `AllowOutOfOrder` flag |
| `P1_PostgreSqlTypeConventionsTests` | `PostgreSqlTypeConventionsTests` | DAL-012 (`TIMESTAMPTZ`, plain `NOW()`) and DAL-013 (`TEXT` replaces arbitrary `VARCHAR(n)`) for PostgreSQL templates, plus the TOML Version / `v_repository_version` consistency invariant |
| `P1_ProductCheckInsertTests` | `ProductCheckInsertTests` | `Repository_Product_CheckInsert` feature: TemplateType enum membership, MigrationEvent EventId, `MigrationState.ProductId`, `TemplateResultCode.ProductNameEmpty`, and SQL template structural patterns across all 5 engines (NameLower lookup — case-insensitive `ToLowerInvariant()` pre-computed in C#, anti-regression check for removed `@Description` parameter, TOML header DatabaseType/TemplateType) |
| `P1_ProductDefaultsPostConfigureOptionsTests` | `ProductDefaultsPostConfigureOptionsTests` | `ProductDefaultsPostConfigureOptions` merging logic |
| `P1_RayAttributeTests` | `RayEnumAttributeTests`, `RayRangeIntAttributeTests`, `RayConnectionStringAttributeTests`, `RayEncodingAttributeTests`, `RayDirectoryExistsAttributeTests` | Custom validation attributes (`RayAttributes`) |
| `P1_RayMigratorOptionsValidatorTests` | `RayMigratorOptionsValidatorTests` | `RayMigratorOptions` validation rules |
| `P1_RepositoryQueryHelperToSnakeCaseTests` | `RepositoryQueryHelperToSnakeCaseTests` | `RepositoryQueryHelper.ToSnakeCase` mapping (DAL-017): mechanical PascalCase → snake_case conversion with the brand-token `RayMigrator` collapsed to `raymigrator` |
| `P1_RequireRollbackFileValidationTests` | `RequireRollbackFileValidationTests` | `RequireRollbackFile` enforcement |
| `P1_ResolveHashValidationScopeTests` | `ResolveHashValidationScopeTests` | `MigrationService.ResolveHashValidationScope` — resolves effective scope from `ProductOptions` target group lookup with fallback to `File` |
| `P1_ResumeFromBlockTests` | `ResumeFromBlockTests` | Resume-from-block logic for partially completed migrations |
| `P1_RetryHelperCustomPredicateTests` | `RetryHelperCustomPredicateTests` | `RetryHelper` custom predicate overloads (`Func<Exception, (bool isTransient, string? errorCode)>`), error code propagation to `RetryExhaustedException`, and linear backoff callback |
| `P1_RetryHelperTests` | `RetryHelperTests` | Transient error retry logic with configurable retries and delays |
| `P1_RollbackErrorActionTests` | `RollbackErrorActionEnumTests`, `RollbackErrorActionTomlParsingTests`, `RollbackErrorActionOverrideResolutionTests`, `RollbackErrorActionMigSettingsTests`, `RollbackErrorActionFullHierarchyTests`, `RollbackErrorActionDefaultsCopyTests` | `RollbackErrorAction` behavior (Terminate vs Ignore during rollback chain) |
| `P1_SchemaNameValidationTests` | `SchemaNameValidationTests` | Schema name validation against DAL `SupportsSchema` flag |
| `P1_SensitiveDataMaskerTests` | `SensitiveDataMaskerTests` | Connection string and password masking in logs |
| `P1_Sha256Tests` | `Sha256Tests` | SHA-256 hash computation for migration files |
| `P1_SqlBlockEnvironmentVariableTests` | `SqlBlockEnvironmentVariableTests` | `{ENV:VAR}` replacement within SQL blocks |
| `P1_SqliteTypeConventionsTests` | `SqliteTypeConventionsTests` | DAL-020: SQLite templates must not contain the `AUTOINCREMENT` keyword (regression guard — `INTEGER PRIMARY KEY` already provides rowid aliasing) |
| `P1_SqlTemplateStructureTests` | `SqlTemplateStructureTests` | SQL template file structure and placeholder validation |
| `P1_TargetGroupMigrationOrderTests` | `TargetGroupMigrationOrderTests` | `ParseTargetGroupMigrationOrder` (comma-separated CLI parsing), `ValidateAndReorder` (alias validation, case-sensitivity, partial/duplicate lists), TOML integration, and `GetFullExecutionOrder` with custom order |
| `P1_TargetMigrationOrderExecutionTests` | `TargetMigrationOrderExecutionTests` | `TargetMigrationOrder` (Simultaneously vs Successively) execution order |
| `P1_TemplateCacheCfgValidationTests` | `TemplateCacheCfgValidationTests` | `TemplateCache` configuration validation against available templates |
| `P1_TemplateExecutorEnvironmentIdTests` | `TemplateExecutorEnvironmentIdTests` | `TemplateExecutor` parameter binding for the EnvironmentId FK feature: verifies the five flipped methods bind `@EnvironmentId` (int) and not a text `@Environment` parameter to `IDal` |
| `P1_TryFinalizeCompletedMigrationTests` | `TryFinalizeCompletedMigrationTests` | Migration finalization (status updates after execution) |
| `P1_UseCliToolAliasInheritanceTests` | `UseCliToolAliasInheritanceTests` | `UseCliToolAlias` inheritance cascade via `ProductDefaultsPostConfigureOptions.MergeDefaults` (ProductDefaults -> Product -> TargetGroup -> Target, explicit values not overridden) |

### P2 — Secondary Features and Utilities

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P2_EnsureConnectionStringOptionsTests` | `EnsureConnectionStringOptionsTests` | `EnsureConnectionStringOptions` in `DalMariaDb` and `DalMySql` — verifies that `AllowUserVariables=true` is injected into connection strings |
| `P2_EnvironmentIdLoggingPipelineTests` | `EnvironmentIdLoggingPipelineTests` | EnvironmentId propagation through the logging pipeline: `MigrationContextEnricher` emits both `EnvironmentId` (int) and `Environment` (text) properties; `DatabaseLogWriter.EnqueueLogEntry` null-guards (passes null when `environmentId == 0`, the actual int otherwise) |
| `P2_FixCommandTests` | `FixIssuesRequestModelTests`, `FixIssuesResultModelTests`, `OrphanedRunInfoModelTests`, `FixIssuesEnumTests`, `FixCommandTemplateTypeTests`, `FixCommandSqlTemplateStructureTests`, `FixCommandConsoleOptionsTests` | `Fix` command logic (orphaned run detection and repair) |
| `P2_MigrationAlreadyRunningTests` | `MigrationAlreadyRunningTests` | Running migration run guard (prevents concurrent executions) |
| `P2_MigrationFileSqlLoggingTests` | `MigrationFileSqlLoggingTests` | SQL content logging during migration execution |
| `P2_MigrationIdLoggingPipelineTests` | `MigrationRecordIdLoggingPipelineTests` | MigrationRecordId enrichment in Serilog log context |
| `P2_MySqlMariaDbIdentifierCasingTests` | `MySqlMariaDbIdentifierCasingTests` | DAL-018 regression guard: zero backtick-quoted PascalCase identifiers in any of the 18 MySQL/MariaDB templates (outside TOML/comments and SELECT aliases); confirms reader-output SELECT templates expose the expected number of PascalCase output aliases (Strategy B) |
| `P2_OptionsEnumPropertyTests` | `OptionsEnumPropertyTests` | Enum property validation in options classes |
| `P2_PostgreSqlIdentifierCasingTests` | `PostgreSqlIdentifierCasingTests` | DAL-017 regression guard: zero double-quoted PascalCase identifiers in any of the 18 PostgreSQL templates (outside TOML/comments and SELECT aliases); confirms reader-output SELECT templates expose the expected number of PascalCase output aliases (Strategy B) |
| `P2_RayMigratorDatabaseSinkTests` | `RayMigratorDatabaseSinkTests` | `RayMigratorDatabaseSink.Emit()` run-mode filter — database logging only fires in Migrate mode (RunModeId=100); early-pipeline logs without RunModeId (null) pass through |
| `P2_StringExtensionsTests` | `StringExtensionsPathTests`, `PlaceholderReplacementTests`, `GetFileEncodingTests` | String extension methods (SHA-256, path parsing, connection string utilities) |
| `P2_TemplateResultCodeTests` | `TemplateResultCodeTests` | Template execution result code interpretation |
| `P2_ToDetailStringMaskingTests` | `ToDetailStringMaskingTests` | `ToDetailString()` output masking for sensitive data |

### P3 — Models and Exceptions

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P3_BaselineAndInfoModelTests` | `BaselineRequestModelTests`, `BaselineResultModelTests`, `MigrationStatusModelTests`, `MigrationHistoryModelTests` | Baseline and Info result model correctness |
| `P3_ModelAndExceptionTests` | `MigrationFileInfoModelTests`, `CustomExceptionTests` | Domain model and custom exception constructors/properties |

### Non-Priority — Exhaustiveness Guards

| Test Class | Description |
|------------|-------------|
| `MigrationCommandExhaustivenessTests` | Ensures every `MigrationCommand` enum value is handled in the engine pipeline command set; fails when a new enum value is added without updating the dispatch logic |

## Helper Classes

### TestFactories

Static factory in `Helpers/TestFactories.cs` for creating test objects without full DI setup:

- `CreateMigrationFile(...)` — creates a `MigrationFileInfo` with sensible defaults
- `CreateMigrationRecord(...)` — creates a `MigrationRecord` with sensible defaults
- `CreateUninitializedMigrationService()` — creates a `MigrationService` via `RuntimeHelpers.GetUninitializedObject` with `NullLogger` injected (for testing internal methods via reflection)
- `CreateMigrationServiceWithCapturingLogger()` — creates a `MigrationService` with a `CapturingLogger` for asserting on log entries

### CapturingLogger

`ILogger<T>` implementation in `Helpers/CapturingLogger.cs` that captures all log entries in a `List<LogEntry>` for assertion. Each `LogEntry` records `LogLevel`, `Message`, and optional `Exception`.

## Dependencies

| Package | Purpose |
|---------|---------|
| xunit.v3 3.2.2 | Test framework (xUnit v3) |
| xunit.runner.visualstudio 3.1.5 | Visual Studio test runner integration |
| AwesomeAssertions 9.5.0 | Fluent assertion syntax |
| NSubstitute 5.3.0 | Mocking framework |
| Microsoft.NET.Test.Sdk 18.9.0 | Test runner integration |
| Microsoft.Extensions.Configuration 10.0.11 | Configuration support |
| Microsoft.Extensions.Configuration.Json 10.0.11 | JSON configuration file support |

## Writing New Unit Tests

1. Choose the appropriate priority prefix (`P0_` through `P3_`) based on the feature's criticality
2. Create the test class in the project root (not in subdirectories)
3. Use `[Fact]` for single-case tests and `[Theory]` with `[InlineData]` for parameterized tests
4. Use `TestFactories` for creating test objects that need `MigrationFileInfo`, `MigrationRecord`, or `MigrationService` instances
5. For testing internal methods on `MigrationService`, use `TestFactories.CreateUninitializedMigrationService()` and invoke methods via reflection
6. For asserting on log output, use `TestFactories.CreateMigrationServiceWithCapturingLogger()`

## Related Documentation

- [Engine Tests](engine-tests.md)
- [Test Infrastructure](test-infrastructure.md)
