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
- DAL factory discovery and caching (and the message that names a DAL assembly whose types did not load), DAL type mapping, engine-specific parameter handling, DAL base transient error detection, and DAL `CreateConnection` ADO.NET type verification
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
- Configuration help texts and the path registry of the Shared package (`ConfigurationHelp`, `JsonPathRegistry`)
- Repository schema service (`RepositorySchemaService`) on a SQLite file, and the table prefix in every index and constraint name of the schema templates and in the testing helpers
- Cancellation of a run: the DAL turns a cancelled call into an `OperationCanceledException` without a further retry, the template executor hands the token to the DAL, `RunCancellation`, the results and messages of a cancelled run, a CLI tool that exits or fails around the cancel, `--cancelled-action`, Ctrl+C and SIGTERM in the CLI and exit code 130
- The refusal of a migrate-up whose file stopped in the middle of its rollback (`MigrationService.FindIncompleteRollback`)
- The pending list of the status (`PendingMigrationReason`, the `info` output) and the mapping of `StartedAt` and `FinishedAt` of the record and history selects as UTC
- Repository maintenance service (`RepositoryMaintenanceService`) on a SQLite file, its executor without the retries of the repository, and the four retention templates of every DAL
- The initialization of the database log, which does not retry a timeout of `DatabaseLogging_CheckCreate`

The project contains **3037 unit test cases**, every theory row counted (**1879 test methods**), organized into **190 test classes** across **150 source test files** using a **P0-P3 priority system** (each file may contain multiple related test classes):

- **P0** — Critical parsing and core behavior (21 files, 28 classes, 281 test methods)
- **P1** — Business logic and configuration (101 files, 119 classes, 1328 test methods)
- **P2** — Secondary features and utilities (25 files, 35 classes, 224 test methods)
- **P3** — Models and exceptions (2 files, 7 classes, 45 test methods)

Additionally, there is 1 test class outside the priority system (`MigrationCommandExhaustivenessTests`) that ensures every `MigrationCommand` enum value is handled in the engine pipeline. The `Helpers/` directory contains 3 non-test files (`TestFactories.cs`, `CapturingLogger.cs`, `ProfileTestContext.cs`).

## ConfigWizard.Core Unit Tests

A separate **ConfigWizard.Core unit test project** (`Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core`) contains **478 tests** across **30 test classes** in **30 files**, covering the zero-dependency shared domain library:

| Test File | Description |
|-----------|-------------|
| `CliToolInheritanceAndSerializerTests.cs` | CLI tool inheritance walking via `WizardValidationInputAdapter` and serializer skip-rules when no effective alias is set |
| `CliToolModelValidationTests.cs` | CLI tool model validation |
| `CliToolPresetProviderTests.cs` | CLI tool preset catalog |
| `ConfigFileMergerTests.cs` / `ConfigFileMergerAdditionalTests.cs` | JSON merge semantics |
| `ConfigFileMergerSharedTests.cs` | The wizard reads the hierarchy with the engine's code: golden merge cases, byte-identical output with the shared merger, reader options, classification through the shared chain (#23) |
| `HierarchyFactoringTests.cs` | Placement principle of the export (#23 Part B): one test per level, conflicts, product-owned elements, CLI tools, non-alias arrays, golden export cases with round trip, no-repetition and minimality properties |
| `ConfigurationFileParserTests.cs` / `ConfigurationFileParserAdditionalTests.cs` | Config file role/pattern parsing |
| `ConfigurationScaffolderTests.cs` | Migration directory scaffold generation |
| `ConfigurationSerializerTests.cs` / `ConfigurationSerializerAdditionalTests.cs` | JSON load/save round-trips |
| `ConfigurationValidatorTests.cs` / `ConfigurationValidatorProductTests.cs` / `ConfigurationValidatorCrossFieldTests.cs` | Validation rules including cross-field structural rules (duplicate aliases, semantic contradictions, CLI tool constraints, default cascade completeness) |
| `DefaultsPromoterTests.cs` / `DefaultsPromoterAdditionalTests.cs` / `DefaultsPromoterAcrossModelsTests.cs` | Defaults promotion logic, including cross-model promotion via `PromoteAcrossModels` |
| `EncodingHelpValuesTests.cs` | Every `MigrationFilesEncoding` value that the field help advertises (`ConfigurationHelp` of the Shared package, English and German) passes the wizard validator, both languages list the same values, `UTF-8-BOM` and `ANSI` are rejected (#4) |
| `PromotionEndToEndTests.cs` | End-to-end scenarios verifying the interaction between `DefaultsPromoter` (promotion) and diff-based serialization (`ConfigurationSerializer.ToJson(model, baseModel)`) across all hierarchy levels, including `StopRollbackOnMissingRollbackFile` at both product and target-group level |
| `EnvFileGeneratorTests.cs` / `EnvFileGeneratorAdditionalTests.cs` | example.env generation |
| `EnvironmentSkeletonGeneratorTests.cs` / `EnvironmentSkeletonGeneratorAdditionalTests.cs` | Environment skeleton files |
| `InheritanceResolverTests.cs` / `InheritanceResolverAdditionalTests.cs` | Effective value resolution |
| `OverridableValueTests.cs` | OverridableValue&lt;T&gt; behavior |
| `WizardOnlyChecksEnumCasingTests.cs` | Wizard-only enum checks accept enum values in any case, as the engine does, and still reject typos (#3) |
| `WizardSetupAnswersTests.cs` | Wizard setup answer model |
| `WizardValidationResultTests.cs` | Validation result aggregation |

Target framework: `net10.0` (single target). No Docker or database connection required.

## ConfigWizard.Web Unit Tests

A separate **ConfigWizard.Web unit test project** (`Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web`) contains **355 tests** across **8 test classes** covering service classes of the Blazor WASM standalone configuration wizard:

| Test File | Class | Tests | Description |
|-----------|-------|-------|-------------|
| `EndToEndCliToolValidationTests.cs` | `EndToEndCliToolValidationTests` | ~2 | End-to-end import-three-files scenario verifying RULE_3_8 validation surfaces |
| `JsonHighlightServiceTests.cs` | `JsonHighlightServiceTests` | ~20 | JSON syntax highlighting service |
| `LocalizationServiceTests.cs` | `LocalizationServiceTests` | ~111 | Localization service (DE/EN string resolution) |
| `TermsAcceptanceServiceTests.cs` | `TermsAcceptanceServiceTests` | ~7 | Session-only acceptance of the terms before the first export: not accepted at first, accepting sets the timestamp, a second accept keeps the first, the acceptance note needs an acceptance and carries every evidence field, the terms URLs match the footer localization |
| `WizardHostStepIndexTests.cs` | `WizardHostStepIndexTests` | ~54 | Step index adjustment and `StepperKey` logic in `WizardHost.razor` (CLI Tools step visibility, `OnExpertModeChanged` shift logic) |
| `WizardStateServiceTests.cs` | `WizardStateServiceTests` | ~124 | Central wizard state management (phases, navigation, reset) |
| `WizardStateServiceValidateAllPeModelsTests.cs` | `WizardStateServiceValidateAllPeModelsTests` | ~6 | `WizardStateService.ValidateAll()` aggregation across BaseModel and merged ProductEnvironmentModels |
| `ZipExportServiceTests.cs` | `ZipExportServiceTests` | ~31 | ZIP archive generation from wizard state (JS interop faked) |

Target framework: `net10.0` (single target). No Docker or database connection required. Tests are plain xUnit tests against service classes (no Blazor component rendering via bUnit).

## Validation Rule-Catalog Unit Tests

A separate **Validation unit test project** (`Raycoon.RayMigrator.Tests.Unit.Validation`) contains **107 tests** that exercise the standalone rule catalog in `Raycoon.RayMigrator.Validation`. The project intentionally references **only** the `Validation` project (no Core, no ConfigWizard.Core), enforcing the rule that the catalog stays WASM-safe and zero-dependency.

| Folder / File | Description |
|---------------|-------------|
| `RuleCatalogTests.cs` | Catalog-level guarantees (rule registration, ID uniqueness) |
| `Rules/AliasUniquenessRuleTests.cs` | Duplicate alias detection across products / target groups / targets |
| `Rules/CliToolDefinitionsRuleTests.cs` | CLI tool definition validation (alias presence, executable path, etc.) |
| `Rules/CliToolParametersRuleTests.cs` | CLI tool parameter rules (placeholder/value pairs) |
| `Rules/CliToolReferencesRuleTests.cs` | Cross-references between `UseCliToolAlias` and `CliTools[]` |
| `Rules/ConnectionStringRuleTests.cs` | Connection string presence and shape |
| `Rules/DefaultCascadeRuleTests.cs` | Default cascade completeness |
| `Rules/SchemaRuleTests.cs` | Repository / DatabaseLogging schema constraints: schema name on schemaless and schema engines, lowercase `TableBaseName` and its maximum of 16 bytes in UTF-8 (`RULE_4_1` to `RULE_4_4`) |
| `Rules/SemanticContradictionsRuleTests.cs` | Semantic contradictions (incompatible option combinations) |
| `Rules/TargetGroupMigrationOrderRuleTests.cs` | `TargetGroupMigrationOrder` configuration validation: exact alias comparison with a hint for a case-only match and no RULE_1_11 for that group, RULE_1_13 only for an order that names exactly the only TargetGroup |
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
│   ├── CapturingLogger.cs        # ILogger<T> that captures log entries
│   └── ProfileTestContext.cs     # Minimal options and context for the command profile tests (#6)
├── MigrationCommandExhaustivenessTests.cs
├── P0_CancelledActionOptionTests.cs
├── P0_CliToolConfigTests.cs
├── P0_CommandProfileTests.cs
├── P0_CommandRunModeMappingTests.cs
├── P0_CommandVerbCasingTests.cs
├── P0_ConfigDirTests.cs
├── P0_ConfigurationFileChainTests.cs
├── P0_ConfigurationHelpTests.cs
├── P0_ConfigurationJsonDiffTests.cs
├── P0_ConfigurationJsonMergerTests.cs
├── P0_ConfigurationProductScopeTests.cs
├── P0_EnvironmentVariableReplacerTests.cs
├── P0_FileClassificationTests.cs
├── P0_JsonPathRegistryTests.cs
├── P0_LastMigrationStatusOptionTests.cs
├── P0_LineEndingTests.cs
├── P0_MigrationRunModeExtensionsTests.cs
├── P0_ParseRunModeTests.cs
├── P0_ShouldSkipBlockSplittingTests.cs
├── P0_SplitSqlIntoBlocksTests.cs
├── P0_TomlParsingTests.cs
├── P1_AutoFixOrphanedRunTests.cs
├── P1_BuildMigrationRunSettingsJsonTests.cs
├── P1_CancellationResultTests.cs
├── P1_AtomicSharedConnectionLoggingTests.cs
├── P1_CanUseSharedConnectionTests.cs
├── P1_CliToolExecutionHelpersTests.cs
├── P1_CliToolExecutorTests.cs
├── P1_CliToolStdinEncodingTests.cs
├── P1_CliToolValidationTests.cs
├── P1_ConfigExportCasesTests.cs
├── P1_ConnectionStringPlaceholderValidationTests.cs
├── P1_ConnectionValidatorProfileTests.cs
├── P1_ConsoleCancellationTests.cs
├── P1_DalBaseTypeAndParameterTests.cs
├── P1_DalCancellationTests.cs
├── P1_DalCreateConnectionTests.cs
├── P1_DalFactoryTests.cs
├── P1_DalIsTransientTests.cs
├── P1_DalParameterEscapingTests.cs
├── P1_DalParameterSubstitutionTests.cs
├── P1_DatabaseLoggingDefaultsTests.cs
├── P1_DatabaseLogInitRetryTests.cs
├── P1_RepositoryReadModelTests.cs
├── P1_DalShippedTransientErrorCodesTests.cs
├── P1_DalSqlServerKilledSessionTests.cs
├── P1_DalSqliteForeignKeysTests.cs
├── P1_DalSqliteOpenTransactionTests.cs
├── P1_DalSqliteParameterTests.cs
├── P1_DalSqlServerOverrideTests.cs
├── P1_DalTransientErrorCodesTests.cs
├── P1_DataAnnotationFailureNamingTests.cs
├── P1_EnvironmentCheckInsertTests.cs
├── P1_RepositorySchemaTests.cs
├── P1_RepositorySchemaServiceTests.cs
├── P1_RunInitiatorTests.cs
├── P1_RepositorySelectTemplateTests.cs
├── P1_EnvironmentIdFkTests.cs
├── P1_EnvironmentResolverTests.cs
├── P1_ExitCodeMatcherTests.cs
├── P1_FilterAlreadyMigratedFilesTests.cs
├── P1_FilterByTargetGroupTests.cs
├── P1_FilterByTargetReleaseTests.cs
├── P1_FlatLayoutAmbiguityTests.cs
├── P1_HandleMigrationErrorBehaviorTests.cs
├── P1_HighestMigratedReleaseTests.cs
├── P1_IncompleteRollbackTests.cs
├── P1_JsonOptionsSourceAliasMergeTests.cs
├── P1_JsonOptionsSourceProductScopeTests.cs
├── P1_MigrateDownSelectionTests.cs
├── P1_MigrationContextCloneTests.cs
├── P1_MigrationContextFactoryTests.cs
├── P1_MigrationContextGuardTests.cs
├── P1_MigrationErrorActionIgnoreTests.cs
├── P1_MigrationErrorActionInheritanceTests.cs
├── P1_MigrationFileEncodingTests.cs
├── P1_MigrationRecordMappingTests.cs
├── P1_MigrationSafetyWarningTests.cs
├── P1_MigSettingsInheritanceTests.cs
├── P1_MissingSerilogSectionMessageTests.cs
├── P1_MigrationRunHeartbeatTests.cs
├── P1_MySqlMariaDbTypeConventionsTests.cs
├── P1_OptionsEnumCaseInsensitivityTests.cs
├── P1_OrphanRepairProjectionTests.cs
├── P1_OutOfOrderDetectionTests.cs
├── P1_PendingMigrationClassificationTests.cs
├── P1_PostgreSqlTypeConventionsTests.cs
├── P1_ProductCheckInsertTests.cs
├── P1_ProductDefaultsPostConfigureOptionsTests.cs
├── P1_ProductScopeOfAHostTests.cs
├── P1_RayAttributeTests.cs
├── P1_RayMigratorOptionsPreparerTests.cs
├── P1_RayMigratorOptionsValidatorTests.cs
├── P1_RepositoryMaintenanceExecutorTests.cs
├── P1_RepositoryMaintenanceServiceTests.cs
├── P1_RepositoryMaintenanceTemplateTests.cs
├── P1_RepositoryQueryHelperToSnakeCaseTests.cs
├── P1_RepositoryTemplatePrefixTests.cs
├── P1_RequireRollbackFileValidationTests.cs
├── P1_ResolveHashValidationScopeTests.cs
├── P1_ResumeFromBlockTests.cs
├── P1_RetryHelperCustomPredicateTests.cs
├── P1_RetryHelperTests.cs
├── P1_RollbackErrorActionTests.cs
├── P1_RunCancellationTests.cs
├── P1_SchemaNameValidationTests.cs
├── P1_SensitiveDataMaskerTests.cs
├── P1_Sha256Tests.cs
├── P1_SqlBlockEnvironmentVariableTests.cs
├── P1_SqliteRunLockTests.cs
├── P1_SqliteTypeConventionsTests.cs
├── P1_SqlTemplateStructureTests.cs
├── P1_TargetGroupMigrationOrderTests.cs
├── P1_TargetMigrationOrderExecutionTests.cs
├── P1_TargetMigrationOrderNamesTests.cs
├── P1_TemplateCacheCfgValidationTests.cs
├── P1_TemplateExecutorCancellationTests.cs
├── P1_TemplateExecutorEnvironmentIdTests.cs
├── P1_TemplateExecutorRepositoryCreatedLoggingTests.cs
├── P1_TemplateExecutorRunDetailsTests.cs
├── P1_TestingHelpersPrefixTests.cs
├── P1_TransientErrorCodesFileTests.cs
├── P1_TryFinalizeCompletedMigrationTests.cs
├── P1_UseCliToolAliasInheritanceTests.cs
├── P2_AssemblyInfoHelperBannerTests.cs
├── P2_AssemblyInfoHelperVersionTests.cs
├── P2_CancelledExitCodeTests.cs
├── P2_DbLogEnabledLoggingPipelineTests.cs
├── P2_EncodingSupportTests.cs
├── P2_EnsureConnectionStringOptionsTests.cs
├── P2_EnvironmentIdLoggingPipelineTests.cs
├── P2_FixCommandTests.cs
├── P2_HashComparisonTests.cs
├── P2_InfoPendingOutputTests.cs
├── P2_MigrationAlreadyRunningTests.cs
├── P2_MigrationFileSqlLoggingTests.cs
├── P2_MigrationHistoryOperationTests.cs
├── P2_MigrationIdLoggingPipelineTests.cs
├── P2_MigrationRunResultTests.cs
├── P2_MySqlMariaDbIdentifierCasingTests.cs
├── P2_OptionsEnumPropertyTests.cs
├── P2_PostgreSqlIdentifierCasingTests.cs
├── P2_RayMigratorDatabaseSinkTests.cs
├── P2_StringExtensionsTests.cs
├── P2_TemplateResponseMultiCodeTests.cs
├── P2_TemplateResultCodeTests.cs
├── P2_ToDetailStringMaskingTests.cs
├── P2_UpdateHashExitCodeTests.cs
├── P2_ValidateHashExitCodeTests.cs
├── P3_BaselineAndInfoModelTests.cs
├── P3_ModelAndExceptionTests.cs
└── GlobalUsings.cs
```

## Test Classes by Priority

### P0 — Critical Parsing and Core Behavior

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P0_CancelledActionOptionTests` | `CancelledActionOptionTests` | `--cancelled-action` (`-ca`) on migrate-up, migrate-down and baseline only: every value in every spelling and through `{ENV:VAR}`, `terminate` by default and in the help, case-insensitive, an unknown value is a parse error instead of a silent `Terminate`, no other command takes the option, its alias collides with no other option, the values cover every `MigrationCancelledAction` |
| `P0_CliToolConfigTests` | `CliToolOptionsInputModeTests` | `CliToolOptions.InputModeEnum` parsing (null, empty, valid, invalid, case-sensitive behavior) |
| `P0_ConfigDirTests` | `ConfigDirTests` | `--config-dir` CLI option and `JsonOptionsSource` configDir parameter: null/empty/whitespace fallback to CWD, non-existent directory validation, file resolution from custom directory, `{ENV:VAR}` resolution, `CommandLineConfiguration` parsing with `--config-dir` and `-cd` aliases across all commands |
| `P0_ConfigurationFileChainTests` | `ConfigurationFileChainTests` | `ConfigurationFileChain` (Shared): the four hierarchy file names in merge order, `TryClassify` round trip, product versus environment for single-segment names, rejection of unrelated names (#23) |
| `P0_ConfigurationJsonMergerTests` | `ConfigurationJsonMergerTests` | `ConfigurationJsonMerger` (Shared): alias-keyed arrays merge by alias (case-insensitive, nested, base order kept, new aliases appended, omitted elements kept), other arrays replaced, explicit null, key casing, four-file precedence, arguments untouched, comments and trailing commas (#23) |
| `P0_ConfigurationProductScopeTests` | `ConfigurationProductScopeTests` | `ConfigurationProductScope` (Shared, ADR-026): `Isolate` keeps the elements of `Products` whose alias equals the product (without case, spelling kept), duplicates of that alias and every other section and key, drops the other products and elements without a string alias, leaves the input unchanged and documents without a `Products` array as they are; `ProductAliases` in order |
| `P0_ConfigurationHelpTests` | `ConfigurationHelpTests` | `ConfigurationHelp` (Shared): every section and field help key resolves in English and German, unknown keys yield null, `JsonPath` attached |
| `P0_JsonPathRegistryTests` | `JsonPathRegistryTests` | `JsonPathRegistry` (Shared): config path and inheritance metadata per field key, every non-concept help key registered, reverse lookup of a path |
| `P0_ConfigurationJsonDiffTests` | `ConfigurationJsonDiffTests` | `ConfigurationJsonDiff` (Shared): `Diff` yields the smallest document that merges back to the target, `TryCommon` the part all documents agree on, both alias-aware; round trip over the golden merge cases (#23 Part B) |
| `P0_EnvironmentVariableReplacerTests` | `EnvironmentVariableReplacerTests` | `{ENV:VAR}` placeholder replacement in configuration values |
| `P0_FileClassificationTests` | `FileClassificationTests` | Migration file discovery, rollback file matching, environment/target filtering |
| `P0_LineEndingTests` | `LineEndingExtractTomlAndSqlTests`, `LineEndingParseTomlConfigTests`, `LineEndingSplitSqlIntoBlocksTests`, `LineEndingHashSensitivityTests` | Line ending normalization across platforms (CRLF, LF, CR) |
| `P0_MigrationRunModeExtensionsTests` | `MigrationRunModeExtensionsTests` | `MigrationRunMode` enum extension methods |
| `P0_CommandProfileTests` | `CommandProfileTests` | Command × side-effect matrix of `MigrationCommandExtensions.GetProfile()`; every `MigrationCommand` except `None` has a profile, `None` throws (#6) |
| `P0_ParseRunModeTests` | `ParseRunModeTests` | `ParseRunMode()` accepts migrate/simulate/validate case-insensitively and throws for anything else; `-rm bogus` is a parse error (#6) |
| `P0_LastMigrationStatusOptionTests` | `LastMigrationStatusOptionTests` | `fix --last-migration-status` (`-lms`) has no default: without it `FixAssumedMigrationStatus` stays null and the repair keeps the progress of every open record; `migrated` and `not-migrated` in both spellings set that status; an unknown value is a parse error, and `ParseLastMigrationStatus` throws instead of falling back |
| `P0_ShouldSkipBlockSplittingTests` | `ShouldSkipBlockSplittingTests` | `MigrationService.ShouldSkipBlockSplitting` — CLI tools execute files as single units, bypassing SQL block splitting |
| `P0_SplitSqlIntoBlocksTests` | `SplitSqlIntoBlocksTests` | SQL statement splitting by engine separator (`GO` for SqlServer, `;` for others) |
| `P0_TomlParsingTests` | `ExtractTomlAndSqlTests`, `ParseTomlConfigTests`, `ParseTomlEnumTests`, `GetValidEnumValuesTests` | TOML metadata header parsing from migration file comments |

### P1 — Business Logic and Configuration

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P1_AutoFixOrphanedRunTests` | `AutoFixOrphanedRunTests` | Automatic detection and repair of orphaned MigrationRun records |
| `P1_BuildMigrationRunSettingsJsonTests` | `BuildMigrationRunSettingsJsonTests` | `MigrationRunSettingsJson` serialization for MigrationRunMeta |
| `P1_AtomicSharedConnectionLoggingTests` | `AtomicSharedConnectionLoggingTests` | The once-per-run Information line naming the targets that share the repository connection (atomic shared-connection path, #27), `MigrationService.TargetSharesRepositoryConnection` and `InvolvedTargets` |
| `P1_CancellationResultTests` | `CancellationResultTests` | What a cancelled run reports: `MigrationRunResult.Cancelled` (70) and `FileOutcome.Cancelled` (100), the positive engine codes `Cancelled` (1004) and `CancelledRollbackIncomplete` (1005), `ExtractErrorCode` maps `OperationCanceledException` and `TaskCanceledException` to `Cancelled`, the error message names the action, its outcome and the reason, and says why an action was ignored; the message of a failed run whose error recovery a cancel stopped names the failure first, then where the cancel stopped the recovery and what that leaves (`CancelledErrorRecoveryMessage`), the outcome of a migrate-down that a cancel stopped does the same for its chain (`CancelledMigrateDownOutcome`), and the outcome of a cancel action whose rollback did not complete counts the files it rolled back and names the failure or every warning (`IncompleteCancelActionOutcome`) |
| `P1_CanUseSharedConnectionTests` | `CanUseSharedConnectionTests` | `MigrationService.CanUseSharedConnection` static guard — four conditions required for atomic shared-connection path: `UseTransaction=true`, `MigrationErrorAction != Ignore` (ignoreBlockErrors=false), matching `DatabaseType` (case-insensitive), identical `ConnectionString` (ordinal). `DbCommandMaxRetries` is explicitly not a guard condition; retries are handled at file level within the atomic path. |
| `P1_CliToolExecutionHelpersTests` | `ResolveUseCliToolAliasTests`, `ResolveCliToolArgumentsTests` | `MigrationService.ResolveUseCliToolAlias` (file vs target alias precedence) and `MigrationService.ResolveCliToolArguments` (placeholder substitution in CLI argument templates) |
| `P1_CliToolExecutorTests` | `CliToolExecutorTests` | `CliToolExecutor.ExecuteAsync` with real OS processes: File mode, Stdin mode, exit code evaluation (custom success/error codes, unexpected codes), stderr capture, timeout (`CliToolTimeoutException`), nonexistent executable (`CliToolExecutionException`), a tool in Stdin mode that exits without reading its input (judged by its exit code; a success code throws `CliToolExecutionException`), cancellation (a cancel while the tool runs kills the tool and the processes it started, a token cancelled before the start starts no process, a successful tool that exited before the cancel took effect keeps its result, in Stdin mode as well when it read the whole file, a tool that fails while the run is cancelled counts as the cancel, and so does a tool in Stdin mode that succeeds without reading its input, all of them also when the cancel overtakes the exit event of the tool, which the test lets happen through a replaced wait for the exit; a cancel that cuts off the write of the file to a tool that already exited with success, while a process the tool started holds its input open, counts as the cancel as well), and duration measurement. Platform: macOS and Linux only (skipped on Windows). |
| `P1_CliToolValidationTests` | `CliToolValidationTests` | `RayMigratorOptionsValidator` CLI tool validation (duplicate aliases, UseCliToolAlias referencing non-existent tools, case-insensitive alias matching) |
| `P1_ConnectionStringPlaceholderValidationTests` | `ConnectionStringPlaceholderValidationTests` | `RULE_7_3` on the configuration as written (#30): real files through `JsonOptionsSource`, the bound options and `RayMigratorOptionsValidator.GetReport` with `RayMigratorConfigSectionAsWritten`; placeholders are not reported as hardcoded credentials, literal passwords are |
| `P1_ConsoleCancellationTests` | `ConsoleCancellationTests` | The signal logic of the CLI (`ConsoleCancellation`) without real signals: the first Ctrl+C or SIGTERM suppresses the default and cancels with the action of `--cancelled-action`, a second keeps the default so that the process ends, a slow cancel callback of a provider holds back neither the handler nor a second signal, the warning says where the command stops and what a second signal does and is logged in the execution context of the command, a signal after `Dispose` keeps the default, a cancel callback that throws still stops the command with the action of the option, `Listen` registers the handlers and `Dispose` removes them |
| `P1_DalBaseTypeAndParameterTests` | `DalBaseTypeAndParameterTests` | `DalBase` type mapping (`TryGetDbTypeForType`), `TryGetDbSpecificSqlParameter`, `DalParameterList`, and `DalParameter` construction and formatting |
| `P1_DalCancellationTests` | `DalCancellationTests` | Cancellation of the asynchronous `IDal` calls through `DalBase`: whatever a cancelled call throws becomes an `OperationCanceledException` with the caller's token, also when the provider reports the cancel without it; a failure without cancel stays unchanged; the token reaches the DAL; a transient failure after the cancel is not retried and a wait for a retry stops; a token cancelled before the call throws without connecting; a database error that meets the cancel names the provider's message, a cancellation the provider reports adds none; the provider reports a token cancelled before the call itself, so a DAL that dropped the token fails (every DAL, except the shared-connection calls of PostgreSQL, MySQL and MariaDB, whose providers check the unopened connection first; `DalCancellationTests` of the engine tests cover them on an open connection); the SQLite DAL executes nothing for a token cancelled before the call and runs a batch that has started to its end |
| `P1_DalCreateConnectionTests` | `DalCreateConnectionTests` | `DAL.CreateConnection` — each DAL returns the correct concrete ADO.NET connection type in a closed/unopened state; `DalExample` stubs throw `NotImplementedException` |
| `P1_DalFactoryTests` | `DalFactoryTests` | `DalFactory` discovery, caching by connection string, error handling for unknown types, and `RegisteredDalTypes`/`ScanAssemblyForDals`; an assembly whose types do not load, like an external DAL built against an earlier engine, is named with its loader exceptions and the hint to rebuild in the message for an unknown database type |
| `P1_DalIsTransientTests` | `DalIsTransientTests` | `DalBase.IsTransient` base implementation: `TimeoutException`, non-transient exceptions (including `OperationCanceledException`, also with a transient inner exception), and recursive inner-exception traversal |
| `P1_DalParameterEscapingTests` | `DalParameterEscapingTests` | `FormatParameterValue` of the PostgreSQL, MariaDB and MySQL DALs (quotes doubled; backslash kept on PostgreSQL, doubled on MariaDB/MySQL; booleans) and their `SubstituteParameters`, including the per-call `$rm…$` tag the PostgreSQL DAL gives every `DO $$` block |
| `P1_RepositoryReadModelTests` | `RepositoryReadModelTests` | Read model package (ADR-025): `ToSnakeCase`, `RepositoryNaming` per engine (qualified and quoted names, canonical database type, schema rule, `SelectStatement`), `RepositoryRows.ColumnsOf` from the records, `RepositoryRowMapper` with SQLite, SQL Server and MySQL value types, missing column and null reporting, schema version |
| `P1_DatabaseLoggingDefaultsTests` | `DatabaseLoggingDefaultsTests` | Database logging into the repository by default: `DatabaseLoggingDefaults.Apply` (missing or partial section, `Enabled: false`, other database type), `IsDatabaseLoggingEnabled`, PostConfigure, `SerilogFactory` sink by the `Enabled` rule, `DatabaseLogWriter` without `MinimumLevel`, `DatabaseLoggerQueue.WaitForEmptyAsync` |
| `P1_DatabaseLogInitRetryTests` | `DatabaseLogInitRetryTests` | `DatabaseLogWriter.InitDatabaseLoggerAsync` on a scripted DAL: a timeout of `DatabaseLogging_CheckCreate` is tried once and ends with a message that names the index build and `DatabaseLogging.DbCommandTimeoutInSeconds`, a transient failure that is no timeout is retried as the log writes are |
| `P1_DalParameterSubstitutionTests` | `DalParameterSubstitutionTests` | `DalParameterSubstitution.Substitute`, the single-pass substitution the four textual DALs share: a value containing another placeholder is not substituted again, whole-identifier matching (`@Product` vs `@ProductId`, `@@var`, `user@host`), case-sensitive names, session variables untouched |
| `P1_DalShippedTransientErrorCodesTests` | `DalShippedTransientErrorCodesTests` | Drift guard: the `TransientErrorCodes.txt` each DAL ships in `DataAccessLayers/{Type}/` equals the built-in list of its `DalBase` subclass (ADR-022) |
| `P1_DalSqlServerKilledSessionTests` | `DalSqlServerKilledSessionTests` | SQL Server classification of a killed pooled session: 4021 and 596 are transient and clear the connection pool (`RequiresPoolClear`), permanent login errors are not |
| `P1_DalSqliteOpenTransactionTests` | `DalSqliteOpenTransactionTests` | A script with its own `BEGIN IMMEDIATE` that fails before `COMMIT` is rolled back by the DAL, so the pooled connection releases the file's write lock and the next writer succeeds (scalar async and non-query sync) |
| `P1_DalSqliteForeignKeysTests` | `DalSqliteForeignKeysTests` | `DalSqlite.EnsureForeignKeysEnabled` connection-string transformation (DAL-001): appends `Foreign Keys=True` when missing, preserves explicit values |
| `P1_DalSqliteParameterTests` | `DalSqliteParameterTests` | SQLite-specific `FormatParameterValue` and `SubstituteParameters` (null/bool/string escaping) |
| `P1_DalSqlServerOverrideTests` | `DalSqlServerOverrideTests` | SQL Server `ConvertToDbValue` DateTime clamping (pre-1753 dates) and `CreateParameter` string size logic |
| `P1_DalTransientErrorCodesTests` | `DalTransientErrorCodesTests` | `DalBase` transient error code list: built-in defaults until `SetTransientErrorCodes` replaces them completely, case-insensitive comparison, `IsTransientCode` classification, `TransientErrorCodesSource` (ADR-022) |
| `P1_EnvironmentCheckInsertTests` | `EnvironmentCheckInsertTests` | `Repository_Environment_CheckInsert` feature: TemplateType enum membership, MigrationEvent EventId, `MigrationState.EnvironmentId`, `TemplateResultCode.EnvironmentNameEmpty`, and SQL template structural patterns across all 5 engines (NameLower lookup, TOML header DatabaseType/TemplateType) |
| `P1_RepositorySchemaTests` | `RepositorySchemaTests` | The repository schema version contract (ADR-024): `RepositorySchema.CurrentVersion` matches the `Repository_Upgrade_{n}` members of `TemplateType`, every engine ships `Repository_CheckCreate`, every step and `Repository_MigratorMeta_CheckInsert` with the declared parameters, the log events 101/102 exist and are seeded, `-10`/`-11` are no longer catalogued; step 3 adds the lookup row `Cancelled` and the four reader indexes on every engine, and `DatabaseLogging_CheckCreate` creates the two log indexes only when they are missing |
| `P1_RepositorySchemaServiceTests` | `RepositorySchemaServiceTests` | `RepositorySchemaService.EnsureAsync` on a SQLite file, from the repository options alone (no product, no context): a fresh repository gets every schema step and one `MigratorMeta` row, a second call changes nothing, a repository newer than the engine is refused with `RepositorySchemaException`, the documented defaults are written into the options, the created repository is logged at Information level, a missing database type or connection string and an unknown database type are refused with `ConfigurationValidationException`, a token cancelled before the call throws `OperationCanceledException` without touching the database; `AddRayMigratorServices` registers the service as a singleton |
| `P1_RunInitiatorTests` | `RunInitiatorTests` | `RunInitiator`: CI/CD detection from `CI`, `GITHUB_ACTIONS`, `GITLAB_CI`, `TF_BUILD`, `JENKINS_URL`, `TEAMCITY_VERSION` (false/0 do not count), user and machine name, `MigrationRunSource` values and their seeds in `Repository_Upgrade_2` of every engine, the CLI parser recording source, user and host |
| `P1_RunCancellationTests` | `RunCancellationTests` | `RunCancellation`, the handle a host cancels one run with: the first `Cancel` cancels the token and records action, reason and time, later calls keep the first action, exactly one of many concurrent calls wins, an invalid action throws without cancelling, a disposed handle returns false and keeps its token readable, a cancel callback that throws is swallowed (`Cancel` returns true, every other callback runs); `MigrationCancelledAction` mirrors the error actions with their numbers |
| `P1_TemplateExecutorRunDetailsTests` | `TemplateExecutorRunDetailsTests` | `RepositoryMigrationRunInsertAsync` binds operation, source, initiator and host (NULL source and the machine name without a host), `RepositoryMigrationRunUpdateAsync` binds error code and message cut to 4000 characters |
| `P1_MigrationRunHeartbeatTests` | `MigrationRunHeartbeatTests` | `MigrationRunHeartbeat` beats until stopped and survives a failing beat, a beat counts once the repository call it awaits has completed, and the stop waits for the beat in progress; `Repository.HeartbeatIntervalInSeconds` (30) and `OrphanedRunThresholdInMinutes` (2) defaults; `fix --older-than` and `FixIssuesRequest` default 2; every engine ships `Repository_MigrationRun_Heartbeat`, measures `MinutesSinceHeartbeat` in the orphan select and guards the fix with `@ThresholdMinutes` |
| `P1_RepositorySelectTemplateTests` | `RepositorySelectTemplateTests` | `Repository_Product_Select` / `Repository_Environment_Select` (#7): TemplateType membership, MigrationEvent ids 122/123, and per-engine structural checks that the templates exist, are read-only (no INSERT/UPDATE/DELETE/CREATE), look up by `NameLower` and return `0,... not found` for a missing row |
| `P1_EnvironmentIdFkTests` | `EnvironmentIdFkTests` | Structural SQL-template tests for the EnvironmentId FK feature: confirms Environment text column is replaced with EnvironmentId INT FK across all 5 engines, with foreign key names that carry the `{CFG:TableBaseName}` prefix (`fk_{CFG:TableBaseName}MigrationRun_Environment`, `fk_{CFG:TableBaseName}migration_run_environment`), and that all INSERT/SELECT templates bind `@EnvironmentId` (not `@Environment`) |
| `P1_EnvironmentResolverTests` | `EnvironmentResolverTests` | `EnvironmentResolver` decision table: the `--environment` argument is the environment, `DOTNET_ENVIRONMENT` is only cross-checked (a different value exits with 2), a blank argument exits with 3 whether or not the variable is set (#32) |
| `P1_ExitCodeMatcherTests` | `ExitCodeMatcherTests` | `ExitCodeMatcher` parsing and evaluation for CLI tool success/error code lists and range notation (`"0"`, `"0,1"`, `"0..5"`) |
| `P1_FilterAlreadyMigratedFilesTests` | `FilterAlreadyMigratedFilesTests` | Filtering out already-migrated files from the migration plan |
| `P1_FilterByTargetGroupTests` | `FilterByTargetGroupTests` | `--target-group` CLI option filtering logic |
| `P1_FilterByTargetReleaseTests` | `FilterByTargetReleaseTests` | `--to-release` CLI option filtering logic |
| `P1_FlatLayoutAmbiguityTests` | `FlatLayoutAmbiguityTests` | Flat migration directory layout ambiguity detection (`ValidateFlatLayoutAmbiguity`) and target group alias casing validation (`ValidateTargetGroupAliasCasing`) |
| `P1_HandleMigrationErrorBehaviorTests` | `HandleMigrationErrorBehaviorTests` | Error handling dispatch based on `MigrationErrorAction` |
| `P1_IncompleteRollbackTests` | `IncompleteRollbackTests` | `MigrationService.FindIncompleteRollback` and `FindIncompleteRollbacks`: a record `Failed` with some but not all of its rollback blocks is found, one without rollback progress, with every rollback block, of another status or of another target is not, and only the targets a file is pending on count; `IncompleteRollbackMessage` names release, file, target, the rollback blocks and the block with which migrate-down continues |
| `P1_MigrateDownSelectionTests` | `MigrateDownSelectionTests` | `MigrationService.IsRolledBackByMigrateDown`: a `Migrated` record, a record `Failed` or `Executing` with committed blocks and without a rollback, and a rollback that stopped in the middle are rolled back; a record without a committed block, a rollback whose first rollback block failed, one that ran to its end with ignored errors, `NotMigrated` and `Pending` are not. `IsStoppedWithoutCommittedBlock` names the records migrate-down warns about, and `KeptStatusWithoutRequiredRollbackFile` the end of the error message when a required rollback file is missing, per kind of record |
| `P1_MigrationContextCloneTests` | `MigrationContextCloneTests` | Regression coverage for `MigrationContext.Clone`: ensures `EnvironmentId`, `MigratorMetaId`, and `MigrationEvent` propagate to the cloned instance |
| `P1_MigrationContextGuardTests` | `MigrationContextGuardTests` | `MigrationContext` rejects `RunMode = Undefined` / `Command = None`; `MigrationContextFactory.Create` stamps the requested command (#6) |
| `P1_ConnectionValidatorProfileTests` | `ConnectionValidatorProfileTests` | `ConnectionValidator` opens target / logging connections only when the command profile says so — read-only commands start against an unreachable Sqlite target (#6) |
| `P1_MigrationErrorActionIgnoreTests` | `MigrationErrorActionIgnoreParsingTests`, `MigrationErrorActionIgnoreMigSettingsTests`, `MigrationErrorActionIgnoreHandleMigrationErrorTests`, `MigrationErrorActionIgnoreFullHierarchyTests` | `MigrationErrorAction.Ignore` behavior (continue after error) |
| `P1_MigrationErrorActionInheritanceTests` | `MigrationErrorActionOverrideResolutionTests`, `MigrationErrorActionTomlMigSettingsMergeTests`, `MigrationErrorActionMultiLevelMigSettingsTests`, `MigrationErrorActionFullHierarchyTests` | `MigrationErrorAction` inheritance chain (product -> release -> targetgroup -> file) |
| `P1_MigrationRecordMappingTests` | `MigrationRecordMappingTests` | `TemplateExecutor.MapMigrationRecord` for the record and the history select: `StartedAt` and `FinishedAt` read as UTC on every engine (a value without a kind is UTC, a local one is converted, SQLite text is parsed as UTC), a null `FinishedAt`, and a row without the two columns (the select of an external DAL written for an older engine) mapped with null dates |
| `P1_MigrationSafetyWarningTests` | `MigrationSafetyWarningTests` | Safety warnings for dangerous migration configurations |
| `P1_MigSettingsInheritanceTests` | `ParseMigSettingsFileTests`, `LoadMigSettingsDefaultsTests`, `ResolveMigSettingsForFileTests` | `migsettings.txt` inheritance merging (product -> release -> targetgroup) |
| `P1_MissingSerilogSectionMessageTests` | `MissingSerilogSectionMessageTests` | Exit-code-4 message of `DirectModePipeline`: names the missing `Serilog` (or `RayMigrator`) section, lists the loaded files, names the files that were not found only when no loaded file carries a `RayMigrator` section, bootstrap message without file diagnostics, `ExecuteAsync` returns 4 (#31) |
| `P1_MySqlMariaDbTypeConventionsTests` | `MySqlMariaDbTypeConventionsTests` | DAL-014 (`DATETIME` → `TIMESTAMP`, `UTC_TIMESTAMP()` → `CURRENT_TIMESTAMP`) and DAL-015 (explicit CHARSET/COLLATE per engine) for MySQL/MariaDB templates |
| `P1_OutOfOrderDetectionTests` | `DetectOutOfOrderFilesTests` | Out-of-order migration detection and `AllowOutOfOrder` flag |
| `P1_OrphanRepairProjectionTests` | `OrphanRepairProjectionTests` | `MigrationService.ProjectOrphanRepair` applies the rule of `Repository_MigrationRecord_FixOrphaned` to the open records of the open run: `Failed` after some committed blocks, `Migrated` with every block, `NotMigrated` without progress, a rollback `Failed` with its rollback blocks or in its first block and `NotMigrated` when every rollback block ran; the counters and every other value stay, records of other runs and closed records are left alone, and without an open run the records come back as they are |
| `P1_PendingMigrationClassificationTests` | `PendingMigrationClassificationTests` | `MigrationService.ClassifyPendingMigration`: the `PendingMigrationReason` of a pending pair follows what the next migrate-up does with its record: no record and a rolled back one are `NotMigrated`, a failed attempt without a resume point (no committed block, changed blocks, a rollback, every block counted on a `Failed` record) is `Failed`, committed leading blocks are `Resumable` after them (also an `Executing` record of an ended run), an open record of the open run is classified as the orphan repair of the next migrate-up leaves it (`Resumable` after committed blocks, not pending with every block, `NotMigrated` without progress), a RunAlways file is `RunAlways` whatever its record says (migrated, `Failed` or `Executing` after committed blocks, `Executing` with every block of an ended run, an open record of the open run), because migrate-up runs it in full, a file that a CLI tool runs is `Failed` after committed blocks and after every block, because migrate-up neither resumes nor finalizes it, and a changed hash `HashMismatch`; the record of a pair is found with the ordinal alias comparison of migrate-up, so a failed record that names the target in another case leaves the pair `NotMigrated`; a record `Failed` in the middle of its rollback is `RollbackIncomplete` before every other reason, also for a RunAlways file, and one without rollback progress stays `Failed` |
| `P1_PostgreSqlTypeConventionsTests` | `PostgreSqlTypeConventionsTests` | DAL-012 (`TIMESTAMPTZ`, plain `NOW()`) and DAL-013 (`TEXT` replaces arbitrary `VARCHAR(n)`) for PostgreSQL templates |
| `P1_ProductCheckInsertTests` | `ProductCheckInsertTests` | `Repository_Product_CheckInsert` feature: TemplateType enum membership, MigrationEvent EventId, `MigrationState.ProductId`, `TemplateResultCode.ProductNameEmpty`, and SQL template structural patterns across all 5 engines (NameLower lookup — case-insensitive `ToLowerInvariant()` pre-computed in C#, anti-regression check for removed `@Description` parameter, TOML header DatabaseType/TemplateType) |
| `P1_ProductDefaultsPostConfigureOptionsTests` | `ProductDefaultsPostConfigureOptionsTests` | `ProductDefaultsPostConfigureOptions` merging logic |
| `P1_ProductScopeOfAHostTests` | `ProductScopeOfAHostTests` | The product scope of options a host built in memory (ADR-026): `RayMigratorOptionsPreparer.IsolateProduct` keeps every element of the alias and removes the others, refuses an unknown product with the aliases of the options and leaves options without products to the preparer; `Prepare` fails on a broken second product without isolation and passes with it; `MigrationContextFactory.Create` runs the product beside a broken one and names the aliases for an unknown product; `ProductAliasGuard.NotFoundMessage` hints at the case, lists the aliases or says there are none |
| `P1_RayAttributeTests` | `RayEnumAttributeTests`, `RayRangeIntAttributeTests`, `RayConnectionStringAttributeTests`, `RayEncodingAttributeTests`, `RayDirectoryExistsAttributeTests` | Custom validation attributes (`RayAttributes`); `[RayEncoding]` writes `UTF-8` into a value that is left out or empty and exposes it as `DefaultValue` |
| `P1_RayMigratorOptionsValidatorTests` | `RayMigratorOptionsValidatorTests` | `RayMigratorOptions` validation rules |
| `P1_RepositoryMaintenanceExecutorTests` | `RepositoryMaintenanceExecutorTests` | `RepositoryMaintenanceExecutor` on a scripted DAL: every retention template runs with the command timeout and the autocommit of the repository but without its retries; `LogTableExistsAsync` asks `DatabaseLogging_Count` with `@ExistsOnly` = 1, the count of the dry run with 0; a log count without the code of the existence is refused |
| `P1_RepositoryMaintenanceServiceTests` | `RepositoryMaintenanceServiceTests` | `RepositoryMaintenanceService.CleanUpAsync` on a SQLite file, from the repository options alone (ADR-027): a request without a cutoff, with a cutoff that is not UTC, a batch size below one or a negative time budget and invalid options are refused before the database is touched, an infinite time budget cleans without a limit, the documented defaults of `RetentionRequest` and of the options, placeholders resolved, a database without a repository and a repository newer than the engine refused, an older schema version cleaned without an upgrade, a repository without a log table skips the log, also in a dry run, and leaves no table behind, a token cancelled before the call throws `OperationCanceledException`, a write lock held beyond the command timeout fails the first batch after one wait instead of the eight retries of the options; `AddRayMigratorServices` registers the service. The deletion itself is tested on every engine by the engine tests |
| `P1_RepositoryMaintenanceTemplateTests` | `RepositoryMaintenanceTemplateTests` | The four retention templates (ADR-027): their `TemplateType` members, a file per template in every DAL including the Example skeleton, which ships no archive template any more, the declared type and parameters, the rows of open runs spared, the delete batch limited in the dialect of the engine, loaded by the template cache with the placeholders of the repository; `DatabaseLogging_Count` of every DAL takes `@ExistsOnly`; the cutoff formatted invariantly to the whole second, also under th-TH (Buddhist calendar) and fi-FI (a dot as time separator), and a time that is not UTC throws; a count beyond the int range read as a long, a negative code raises the error of the result contract |
| `P1_RepositoryQueryHelperToSnakeCaseTests` | `RepositoryQueryHelperToSnakeCaseTests` | `RepositoryQueryHelper.ToSnakeCase` mapping (DAL-017): mechanical PascalCase → snake_case conversion with the brand-token `RayMigrator` collapsed to `raymigrator` |
| `P1_RepositoryTemplatePrefixTests` | `RepositoryTemplatePrefixTests` | Every `pk_`, `fk_`, `ix_` and `uix_` name in `Repository_CheckCreate`, `Repository_Upgrade_1` to `Repository_Upgrade_3`, `DatabaseLogging_CheckCreate` and the four retention templates of all five engines continues with `{CFG:TableBaseName}` (`pk_{CFG:TableBaseName}migration_run`), so that repositories with different prefixes can share one database or schema; spot checks of single key names per engine |
| `P1_RequireRollbackFileValidationTests` | `RequireRollbackFileValidationTests` | `RequireRollbackFile` enforcement |
| `P1_ResolveHashValidationScopeTests` | `ResolveHashValidationScopeTests` | `MigrationService.ResolveHashValidationScope` — resolves effective scope from `ProductOptions` target group lookup with fallback to `File` |
| `P1_ResumeFromBlockTests` | `ResumeFromBlockTests` | Resume-from-block logic for partially completed migrations; a RunAlways file is never resumed (`ResumableBlockOf` returns 0 without a partial record, and `FindResumableBlock` logs at Debug that the committed blocks of the earlier attempt run again) |
| `P1_RetryHelperCustomPredicateTests` | `RetryHelperCustomPredicateTests` | `RetryHelper` custom predicate overloads (`Func<Exception, (bool isTransient, string? errorCode)>`), error code propagation to `RetryExhaustedException`, and linear backoff callback |
| `P1_RetryHelperTests` | `RetryHelperTests` | Transient error retry logic with configurable retries and delays; a cancel while waiting for a retry stops the wait and starts no retry, a failure after the cancellation is neither classified nor retried |
| `P1_RollbackErrorActionTests` | `RollbackErrorActionEnumTests`, `RollbackErrorActionTomlParsingTests`, `RollbackErrorActionOverrideResolutionTests`, `RollbackErrorActionMigSettingsTests`, `RollbackErrorActionFullHierarchyTests`, `RollbackErrorActionDefaultsCopyTests` | `RollbackErrorAction` behavior (Terminate vs Ignore during rollback chain) |
| `P1_SchemaNameValidationTests` | `SchemaNameValidationTests` | Schema name validation against DAL `SupportsSchema` flag |
| `P1_SensitiveDataMaskerTests` | `SensitiveDataMaskerTests` | Connection string and password masking in logs |
| `P1_Sha256Tests` | `Sha256Tests` | SHA-256 hash computation for migration files |
| `P1_SqlBlockEnvironmentVariableTests` | `SqlBlockEnvironmentVariableTests` | `{ENV:VAR}` replacement within SQL blocks |
| `P1_SqliteRunLockTests` | `SqliteRunLockTests` | The run lock on SQLite under starts at the same moment, on a temporary file in WAL and in rollback journal mode: after the repository is created through `IRepositorySchemaService` and a product and an environment are registered through their templates, eight `DalSqlite` instances of their own run `Repository_MigrationRun_Insert` at once in each of 10 rounds; exactly one gets a run with its `MigrationRunMeta` row, the other seven are refused with `-2`, and one run is open. Before the `BEGIN IMMEDIATE` transaction of the template (0.20.0) several starts took the lock in most rounds, in both journal modes |
| `P1_SqliteTypeConventionsTests` | `SqliteTypeConventionsTests` | DAL-020: SQLite templates must not contain the `AUTOINCREMENT` keyword (regression guard — `INTEGER PRIMARY KEY` already provides rowid aliasing); DAL-024: every SQLite template with several statements carries the comment `Transaction requirement (DAL-024):`, which says that the engine runs it without a transaction of the driver, so that every statement commits on its own unless the template opens `BEGIN IMMEDIATE … COMMIT` itself, a template with one statement does not, and every template is classified as one or the other |
| `P1_SqlTemplateStructureTests` | `SqlTemplateStructureTests` | SQL template file structure and placeholder validation |
| `P1_TargetGroupMigrationOrderTests` | `TargetGroupMigrationOrderTests` | `ParseTargetGroupMigrationOrder` (comma-separated CLI parsing), `ValidateAndReorder` (alias validation, case-sensitivity, partial/duplicate lists, an order of the only TargetGroup ignored with a warning and any other order on it refused), TOML integration, and `GetFullExecutionOrder` with custom order and with an ignored order of the only TargetGroup |
| `P1_TargetMigrationOrderExecutionTests` | `TargetMigrationOrderExecutionTests` | `TargetMigrationOrder` (FileByFile vs TargetByTarget) execution order |
| `P1_TemplateCacheCfgValidationTests` | `TemplateCacheCfgValidationTests` | `TemplateCache` configuration validation against available templates |
| `P1_TemplateExecutorCancellationTests` | `TemplateExecutorCancellationTests` | Every public repository method of `TemplateExecutor` hands its token to every DAL call (one case per method, and a guard that every public method has a case), and a call that the DAL ends as cancelled leaves the executor as the DAL's `OperationCanceledException`, a failure without cancel as `TemplateExecutionException` |
| `P1_TemplateExecutorEnvironmentIdTests` | `TemplateExecutorEnvironmentIdTests` | `TemplateExecutor` parameter binding for the EnvironmentId FK feature: verifies the five flipped methods bind `@EnvironmentId` (int) and not a text `@Environment` parameter to `IDal` |
| `P1_TemplateExecutorRepositoryCreatedLoggingTests` | `TemplateExecutorRepositoryCreatedLoggingTests` | `TemplateExecutor.RepositoryCheckCreateAsync` and the schema steps it runs through `RepositorySchemaExecutor` (ADR-024), against a mocked `IDal`: a fresh repository gets every step and is logged as created at Information level, an older one gets the missing steps and is logged as upgraded, a current one only registers the engine version; tables without a schema version, a newer version and a step that returns another code are refused; version and database type are bound as parameters; schema name masked unless `--reveal-sensitive-data` (#27); a step that fails after a parallel start applied it is accepted on re-read of the version, otherwise the step's own error surfaces |
| `P1_TestingHelpersPrefixTests` | `TestingHelpersPrefixTests` | The testing package with a table prefix, on a SQLite file: `RepositoryQueryHelper` reads the prefixed repository tables, `DatabaseCleanupHelper.CleanDatabase` drops the repository of one prefix and leaves the repository of another prefix alone |
| `P1_TransientErrorCodesFileTests` | `TransientErrorCodesFileTests` | `TransientErrorCodesFile` parser and loader for `TransientErrorCodes.txt`: comments, blank lines, trimming, whitespace inside a code throws with the line number, missing file falls back (ADR-022) |
| `P1_TryFinalizeCompletedMigrationTests` | `TryFinalizeCompletedMigrationTests` | Migration finalization (status updates after execution), detected through `FindFinalizableRecord`; the record of a RunAlways file is never finalized, also not by `TryFinalizeCompletedMigrationAsync` |
| `P1_UseCliToolAliasInheritanceTests` | `UseCliToolAliasInheritanceTests` | `UseCliToolAlias` inheritance cascade via `ProductDefaultsPostConfigureOptions.MergeDefaults` (ProductDefaults -> Product -> TargetGroup -> Target, explicit values not overridden) |
| `P1_JsonOptionsSourceAliasMergeTests` | `JsonOptionsSourceAliasMergeTests` | `JsonOptionsSource` with real files: the merged order and the overrides by alias through `MergeConfigurationFiles`, the loaded product through the section of `LoadAsync` (reversed order, non-alias arrays replaced, `{ENV:}` after the merge), diagnostics from the shared chain, comments accepted, error paths; golden cases under `Testing/ConfigMergeCases/` shared with the wizard suite (#23) |
| `P1_JsonOptionsSourceProductScopeTests` | `JsonOptionsSourceProductScopeTests` | `JsonOptionsSource.LoadAsync` hands the pipeline the product of the run only (ADR-026): the bound section, the section as written and the host configuration carry the shared sections and that product, a placeholder of another product is neither resolved nor reported, an unknown product fails while loading with the aliases of the files, another case of the alias loads the product for the guard, elements without an alias are left out, files without any product alias load as before |
| `P1_ConfigExportCasesTests` | `ConfigExportCasesTests` | The engine's loader reads the file families of the golden export cases (`Testing/ConfigExportCases/`) back to the effective configuration of every combination (#23 Part B) |

### P2 — Secondary Features and Utilities

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P2_CancelledExitCodeTests` | `CancelledExitCodeTests` | Exit codes of `RayMigratorService.DoWorkAsync` after Ctrl+C or SIGTERM: a cancelled run (migrate-up, migrate-down, baseline), and, while the token of the process is cancelled, update-hash and fix with `Cancelled` (1004) and validate-hash and info with an `OperationCanceledException` exit with 130 and log a warning; a cancelled migrate-up whose rollback did not complete, a migrate-up with the result `Error` while the token of the process is cancelled (a failure whose error recovery the cancel stopped), a failure without cancel, update-hash and fix with 1004 but no signal, and an `OperationCanceledException` without the token of the process exit with 1; the run requests carry the handle of the process, every other service call gets its token, and the first Ctrl+C during a run cancels with the action of `--cancelled-action` |
| `P2_EnsureConnectionStringOptionsTests` | `EnsureConnectionStringOptionsTests` | `EnsureConnectionStringOptions` in `DalMariaDb` and `DalMySql` — verifies that `AllowUserVariables=true` is injected into connection strings |
| `P2_EnvironmentIdLoggingPipelineTests` | `EnvironmentIdLoggingPipelineTests` | EnvironmentId propagation through the logging pipeline: `MigrationContextEnricher` emits both `EnvironmentId` (int) and `Environment` (text) properties; `DatabaseLogWriter.EnqueueLogEntry` null-guards (passes null when `environmentId == 0`, the actual int otherwise) |
| `P2_FixCommandTests` | `FixIssuesRequestModelTests`, `FixIssuesResultModelTests`, `OrphanedRunInfoModelTests`, `FixIssuesEnumTests`, `FixCommandTemplateTypeTests`, `FixCommandSqlTemplateStructureTests`, `FixAssumedStatusPassThroughTests`, `FixCommandConsoleOptionsTests`, `FixScopeDispatchTests` | `fix` command logic (orphaned run detection and repair): `AssumedMigrationStatus` is null by default and reaches the repair unchanged from the console options; the `Repository_MigrationRecord_FixOrphaned` templates of every engine set an explicit status, decide per record from its progress (the rollback counter, every block committed) for status 0, set `FinishedAt` only where it is empty and leave `DurationInMs` alone; the `Repository_MigrationRun_FixOrphaned` templates of every engine write `ErrorCode` `1006` and the message of the repair only where the run row has none |
| `P2_InfoPendingOutputTests` | `InfoPendingOutputTests` | `info` prints the pending migrations under their count as a table in the order of the next migrate-up (release, target group, target, file, reason; a resumed file as `Resumable after block n`, a file whose rollback stopped in the middle as `RollbackIncomplete after k of n rollback blocks, run migrate-down`), cuts the list after `PendingListLimit` (50) entries with `… and n more`, prints no list without pending migrations, and names `Pending=` and `LastMigration=` on every target group line |
| `P2_MigrationAlreadyRunningTests` | `MigrationAlreadyRunningTests` | Running migration run guard (prevents concurrent executions) |
| `P2_MigrationFileSqlLoggingTests` | `MigrationFileSqlLoggingTests` | SQL content logging during migration execution |
| `P2_MigrationIdLoggingPipelineTests` | `MigrationRecordIdLoggingPipelineTests` | MigrationRecordId enrichment in Serilog log context |
| `P2_DbLogEnabledLoggingPipelineTests` | `DbLogEnabledLoggingPipelineTests` | `MigrationContextEnricher` emits `DbLogEnabled` from the command profile (info/validate-hash/fix --run-mode simulate/simulate: false; update-hash/baseline/fix/migrate: true) (#6) |
| `P2_MigrationHistoryOperationTests` | `MigrationHistoryOperationTests` | `MigrationOperation.Baseline` (110) and `MigrationService.SummarizeRun()`, which counts the migrations of a run for the info run history from its history rows (#6, #13) |
| `P2_MigrationRunResultTests` | `MigrationRunResultTests` | The `MigrationRunResult` values never move: `PartialSuccess` (50), `Cancelled` (70) and `Recovered` (80) lie between `Running` and `Error`, and every member is seeded into the lookup table of every DAL by the schema step that introduced it (#18) |
| `P2_MySqlMariaDbIdentifierCasingTests` | `MySqlMariaDbIdentifierCasingTests` | DAL-018 regression guard: zero backtick-quoted PascalCase identifiers in any of the 30 MySQL/MariaDB templates (outside TOML/comments and SELECT aliases), lowercase index and constraint names; confirms reader-output SELECT templates expose the expected number of PascalCase output aliases (Strategy B), 23 in the record select and the same in the history select |
| `P2_OptionsEnumPropertyTests` | `OptionsEnumPropertyTests` | Enum property validation in options classes |
| `P2_PostgreSqlIdentifierCasingTests` | `PostgreSqlIdentifierCasingTests` | DAL-017 regression guard: zero double-quoted PascalCase identifiers in any of the 30 PostgreSQL templates (outside TOML/comments and SELECT aliases), lowercase index and constraint names; confirms reader-output SELECT templates expose the expected number of PascalCase output aliases (Strategy B), 23 in the record select and the same in the history select |
| `P2_RayMigratorDatabaseSinkTests` | `RayMigratorDatabaseSinkTests` | `RayMigratorDatabaseSink.Emit()` gate — events carrying `DbLogEnabled = false` are dropped, `true` is enqueued, early-pipeline logs without the property pass through; `RunModeId` is a stamp, not a gate (#6) |
| `P2_StringExtensionsTests` | `StringExtensionsPathTests`, `PlaceholderReplacementTests`, `GetFileEncodingTests` | String extension methods (SHA-256, path parsing, connection string utilities) |
| `P2_TemplateResponseMultiCodeTests` | `TemplateResponseMultiCodeTests` | Template result contract `'code[,code...],message'`: leading integer tokens become `ResultCodes`, the rest stays the message, negative first code still throws (#27) |
| `P2_TemplateResultCodeTests` | `TemplateResultCodeTests` | Template execution result code interpretation; `OrphanedRunClosed` is the positive engine code 1006 and no known template code |
| `P2_ToDetailStringMaskingTests` | `ToDetailStringMaskingTests` | `ToDetailString()` output masking for sensitive data |

### P3 — Models and Exceptions

| Test File | Classes | Description |
|-----------|---------|-------------|
| `P3_BaselineAndInfoModelTests` | `BaselineRequestModelTests`, `BaselineResultModelTests`, `MigrationStatusModelTests`, `MigrationHistoryModelTests`, `PendingMigrationModelTests` | Baseline and Info result model correctness, the `PendingMigrationReason` values (up to `RollbackIncomplete` 60) and `PendingMigrationInfo`, which carries the rollback blocks for `RollbackIncomplete` only |
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
| NSubstitute 6.2.0 | Mocking framework |
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
