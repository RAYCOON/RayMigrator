# Configuration Validation Rules

This is the **single source of truth** for all configuration validation rule codes emitted by RayMigrator. Both the **Engine** (at host startup via `RayMigratorOptionsValidator`) and the **ConfigWizard** (in the Overview panel) run the identical catalog, so the same rule code surfaces in both places for the same misconfiguration.

Implementation lives in the `Raycoon.RayMigrator.Validation` project:
- Rule classes: `Raycoon.RayMigrator.Validation/Rules/*.cs`
- Rule identifiers: `Raycoon.RayMigrator.Validation/RuleIds.cs`
- Message templates: `Raycoon.RayMigrator.Validation/Messages/ValidationMessages.cs`
- Entry point: `RuleCatalog.RunAll(ValidationInput)` returns a `ValidationReport`.

## Severity

- **Error** — blocks engine startup (`OptionsValidationException` on `ValidateOnStart`) and is rendered as a red badge in the ConfigWizard Overview.
- **Warning** — logged via `Serilog.Log.Warning` at engine startup, and rendered as a yellow badge in the ConfigWizard Overview. Does not block startup.

## Rule Index

### Group 1 — Alias uniqueness & migration order

| Code | Name | Severity | What it checks |
|------|------|----------|----------------|
| RULE_1_1 | DUPLICATE_TARGETGROUP_ALIAS | Error | TargetGroup aliases within a single Product must be unique (case-insensitive). |
| RULE_1_2 | DUPLICATE_TARGET_ALIAS | Error | Target aliases within a single TargetGroup must be unique (case-insensitive). |
| RULE_1_8 | DUPLICATE_PRODUCT_ALIAS | Error | Product aliases must be unique across the configuration (case-insensitive). |
| RULE_1_9 | DUPLICATE_CLITOOL_ALIAS | Error | CliTool aliases must be unique across the configuration (case-insensitive). |
| RULE_1_10 | TG_MIGRATION_ORDER_INVALID_ALIAS | Error | Every alias listed in `Product.TargetGroupMigrationOrder` must match a real TargetGroup in that Product. |
| RULE_1_11 | TG_MIGRATION_ORDER_MISSING_ALIAS | Error | Every TargetGroup in the Product must appear in `TargetGroupMigrationOrder` when it is set. |
| RULE_1_12 | TG_MIGRATION_ORDER_DUPLICATE_ALIAS | Error | An alias must not appear more than once in `TargetGroupMigrationOrder`. |
| RULE_1_13 | TG_MIGRATION_ORDER_IRRELEVANT_FOR_SINGLE_TG | Warning | Setting `TargetGroupMigrationOrder` on a Product that has only one TargetGroup has no effect. |

### Group 2 — Semantic contradictions

| Code | Name | Severity | What it checks |
|------|------|----------|----------------|
| RULE_2_11 | ROLLBACK_WITHOUT_ROLLBACK_ERROR_ACTION | Error | If the effective `MigrationErrorAction` is `Rollback`, `RollbackErrorOnly`, or `RollbackRelease`, then an effective `RollbackErrorAction` must be defined at product or defaults level. |
| RULE_2_13 | EXTENSION_EQUALS_PRE_EXTENSION | Error | `MigrationFilesExtension` and `MigrationRollbackFilesPreExtension` must resolve to different values (case-insensitive) or rollback-file discovery breaks. |

### Group 3 — CLI tool rules

| Code | Name | Severity | What it checks |
|------|------|----------|----------------|
| RULE_3_1 | FILE_MODE_MISSING_FILEPATH | Error | When `InputMode=File`, the `ArgumentTemplate` must contain `{FilePath}`. |
| RULE_3_2 | STDIN_MODE_WITH_FILEPATH | Warning | When `InputMode=Stdin`, `{FilePath}` in `ArgumentTemplate` is unused and will not be resolved. |
| RULE_3_3 | USE_CLI_TOOL_ALIAS_INVALID | Error | Every `UseCliToolAlias` reference (at ProductDefaults / Product / TargetGroup / Target) must match a defined CliTool. |
| RULE_3_4 | CLI_PARAMS_WITHOUT_CLI_ALIAS | Warning | A Target defines `CliToolParameters` but no `UseCliToolAlias` resolves at any level — the parameters are dead config. |
| RULE_3_7 | EXIT_CODE_EXPRESSION_INVALID | Error | Every expression in `SuccessExitCodes` must parse as a single integer (`"0"`), closed range (`"1..5"`), open-up range (`"10.."`), or open-down range (`"..-1"`). |
| RULE_3_8 | CLI_PARAMS_MISSING_REQUIRED_KEYS | **Error** | When a Target resolves to a CLI tool, every placeholder in that tool's `ArgumentTemplate` (except reserved `{FilePath}`) must have a non-empty entry in the effective `CliToolParameters` map. Severity upgraded from Warning to Error — missing values crash the CLI at runtime. |
| RULE_3_9 | CLI_PARAMS_RESERVED_KEY_COLLISION | Error | `CliToolParameters` must not contain a reserved key (currently `FilePath`). Reserved keys are substituted internally by the engine. |
| RULE_3_10 | CLI_PARAMS_UNUSED_KEYS | Warning | Keys in `CliToolParameters` that do not appear as placeholders in the tool's `ArgumentTemplate` are dead config — likely a typo or leftover after a template change. |

### Group 4 — Schema / lowercase identifier rules

| Code | Name | Severity | What it checks |
|------|------|----------|----------------|
| RULE_4_1 | SCHEMA_ON_SCHEMALESS_DB | Warning | Setting `SchemaName` for `Sqlite` has no effect — SQLite has no schema concept. |
| RULE_4_2 | SCHEMA_MISSING_FOR_SCHEMA_DB | Error | `Repository.SchemaName` is required for `SqlServer` and `PostgreSQL`. Also flags a missing `Repository` section or missing `DatabaseType`/`ConnectionString`. |
| RULE_4_3 | LOWERCASE_TABLEBASENAME_REQUIRED | Error | `TableBaseName` must be all-lowercase for `PostgreSQL`, `MariaDb`, and `MySql` — unquoted PostgreSQL identifiers fold to lowercase, and the MariaDB/MySQL repository schema is stored as lowercase. |

### Group 7 — Connection-string hygiene

| Code | Name | Severity | What it checks |
|------|------|----------|----------------|
| RULE_7_1 | REPO_AND_TARGET_SAME_DB | Warning | Repository and a migration Target share the same `ConnectionString` — Single Point of Failure. |
| RULE_7_2 | DUPLICATE_TARGET_CONNECTION | Warning | Two Targets in the same TargetGroup share a `ConnectionString` — migrations would run twice on the same database. |
| RULE_7_3 | HARDCODED_CREDENTIALS | Warning | A `ConnectionString` contains `Password=...` or `Pwd=...` not wrapped in `{ENV:VAR}`. Consider moving the credential to an environment variable. |

### Group 8 — Default-cascade completeness

| Code | Name | Severity | What it checks |
|------|------|----------|----------------|
| RULE_8_1 | MISSING_EFFECTIVE_MIGRATION_ERROR_ACTION | Error | After the ProductDefaults → Product cascade, every Product must have a `MigrationErrorAction`. |
| RULE_8_2 | MISSING_EFFECTIVE_MIGRATION_ORDER | Error | After the ProductDefaults.TargetGroupDefaults cascade, every TargetGroup must have a `TargetMigrationOrder`. |
| RULE_8_3 | MISSING_EFFECTIVE_HASH_VALIDATION_SCOPE | Error | After the ProductDefaults.TargetGroupDefaults cascade, every TargetGroup must have a `HashValidationScope`. |

## Engine-only additive checks

These run **outside** the shared catalog because they rely on APIs that are unavailable in Blazor WebAssembly:

- `RayConnectionStringAttribute` — uses `System.Data.Common.DbConnectionStringBuilder` to parse `ConnectionString` syntax (applied via `[RayConnectionString]` DataAnnotation on `TargetOptions.ConnectionString`).
- `RayDirectoryExistsAttribute` — verifies filesystem existence of directory paths.
- `SchemaNameValidator` — pipeline-level DAL-aware check that uses `DalSpecificProperties.SupportsSchema` (runs after DAL discovery).

## Breaking changes vs. previous behaviour

1. **Engine startup now fails on cross-field config errors.** Before this change, `RayMigratorOptionsValidator` was never registered in DI (a latent bug). Starting with this release, misconfigured production configs will fail fast at `host.Build()` with `OptionsValidationException` instead of crashing later during migration. Errors carry `[RULE_x_y]` prefixes and can be looked up above.
2. **Alias comparisons use `OrdinalIgnoreCase`.** The ConfigWizard previously treated `"Backend"` and `"backend"` as two distinct TargetGroups (ordinal-sensitive); the Engine always treated them as duplicates (ordinal-insensitive). Both sides now agree on case-insensitive. Configs relying on case-distinct aliases must rename.
3. **RULE_3_8 severity upgrade.** CLI parameter completeness was previously a Warning in the Wizard. It is now an Error in both Engine and Wizard — missing values crash the CLI at runtime.
4. **`CliToolParameterHelper` deleted.** The ConfigWizard.Core helper was replaced by:
   - `Raycoon.RayMigrator.Validation.Helpers.CliToolPlaceholderExtractor` — for placeholder extraction (WASM-safe, shared).
   - `Raycoon.RayMigrator.ConfigWizard.Core.Services.WizardCliToolParameterResolver` — for wizard-side preset fallback.

## Admin-DB / Studio configs (out of scope)

The `PreBuiltOptions` branch in `DirectModePipeline` (used by RayMigrator-Studio) does **not** invoke `IValidateOptions<RayMigratorOptions>`. If future Studio code needs validation, call `RayMigratorOptionsValidator.Validate(null, preBuiltOptions)` explicitly at the Studio boundary.
