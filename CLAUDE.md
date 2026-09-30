# CLAUDE.md

Guidance for Claude Code when working in this repository. Keep it short; details live in `Docs/`.

## What this is

RayMigrator is a database migration framework for SQL Server, PostgreSQL, MariaDB, MySQL and SQLite. The repository builds the `raymigrator` CLI, the engine as NuGet packages, and a Blazor WebAssembly Config Wizard. A separate repository, **RayMigrator-Studio**, consumes the engine packages (`Core`, `Pipeline`, `OperatingMode.ManagedLocal/ManagedRemote`, `RayMigratorBootstrapOptions`). Verify Studio usage before deleting or changing "unused" public types.

## Rules

1. **English only** in code, comments, commit messages and documentation.
2. **No AI attribution.** Never add `Co-Authored-By: Claude ...`, `Generated with Claude Code` or any other AI reference to commits, PR descriptions, code or comments. This also applies to trailers a tool or harness suggests. Omit them.
3. **Branches.** Work on `develop` or a feature branch based on it. Never commit to `main`; it only receives a fast-forward at release time.
4. **CHANGELOG.** Every behaviour change, fix or new option gets an entry under `[Unreleased]` in `CHANGELOG.md`. Documentation-only and test-only changes do not.
5. **Never modify executed migrations.** Create new migration files instead. This includes the test migration sets under `Testing/MigrationFiles/`.
6. **Follow [Docs/11-development/coding-conventions.md](Docs/11-development/coding-conventions.md)** for new and changed code. Do not bulk-reformat existing code.
7. **Prefer the repo skills and agents** (see below) over ad-hoc shell commands when a task fits one of them.

## Solution map

| Group | Projects | Notes |
|---|---|---|
| CLI | `Raycoon.RayMigrator.Console` | Assembly name `raymigrator`. Commands are defined in Core, not here. |
| Engine (NuGet) | `Core`, `Shared`, `Infrastructure`, `Services`, `Services.Abstractions`, `Pipeline`, `Database`, `Database.Common` | All prefixed `Raycoon.RayMigrator.`. Multi-target `net10.0;net9.0;net8.0`. |
| DAL plugins (NuGet) | `Database.SqlServer`, `Database.PostgreSQL`, `Database.MariaDb`, `Database.MySql`, `Database.Sqlite` | SQL templates in `Templates/*.sql`, copied to `DataAccessLayers/<Db>/` at build. |
| Validation (NuGet) | `Validation` | Shared rule catalog, WASM-safe, zero dependencies. Rules `RULE_<section>_<n>`, see `Docs/appendix/validation-rules.md`. |
| Test support (NuGet) | `Testing` | Reusable integration-test infrastructure. |
| Config Wizard | `ConfigWizard.Core`, `ConfigWizard.Web` | Core has zero dependencies. Web is Blazor WASM, MudBlazor, `net10.0` only, deployed to Azure Static Web Apps. |
| Example | `Database.Example` | MIT-licensed skeleton for external DAL plugins. Not packed. |
| Tests | `Tests.Unit` (~1400), `Tests.Engine` (~1100, Docker), `Tests.Unit.ConfigWizard.Core` (~450), `Tests.Unit.ConfigWizard.Web` (~230), `Tests.Unit.Validation` (~70) | All `net10.0` only. xunit.v3, AwesomeAssertions, NSubstitute. No bUnit. |

There is no `Tests.Integration` project and no Terminal.Gui wizard. Integration tests are `Tests.Engine`.

## Build, test, run

```bash
dotnet build                                                   # all TFMs, needs .NET SDK 10
dotnet build --framework net10.0                               # faster
dotnet test Raycoon.RayMigrator.Tests.Unit/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Unit.Validation/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Engine/ --framework net10.0 --filter "Engine=SqlServer"   # Engine=PostgreSQL|MariaDb|MySql|Sqlite, Category=MigrateUp|MigrateDown|Compound|CliTool|Features

# Docker test databases (containers rm_db_sqlserver 1433, rm_db_postgresql 5432, rm_db_mariadb 3306, rm_db_mysql 3307)
./Testing/Docker/RunDocker.default.all.ps1                     # or .sqlserver / .postgresql / .mariadb / .mysql
cd Testing/Docker && docker compose --env-file default.env --profile all down -v
# SQLite engine tests need no Docker. Engine tests skip themselves when a database is unreachable.

# Run the CLI from source
dotnet run --project Raycoon.RayMigrator.Console --framework net10.0 -- migrate-up --product <Product> -env Docker --run-mode simulate

# Editions: Debug = Development, Release -p:BuildStage=Docker = Partner, Release -p:BuildStage=Production = Professional (CI uses Production)
```

Full details: [Docs/11-development/developer-workflow.md](Docs/11-development/developer-workflow.md) and [Docs/10-testing/](Docs/10-testing/).

## CLI at a glance

Seven commands, lowercase kebab-case: `migrate-up`, `migrate-down`, `validate-hash`, `update-hash`, `info`, `baseline`, `fix`. Every command takes `--product/-p` and `--environment/-env`. `--run-mode/-rm` is `migrate`, `simulate` or `validate`. `migrate-down` requires `--to-release/-tr`. Global options: `--config-dir/-cd`, `--startup-info/-si`, `--reveal-sensitive-data/-rsd`. Any option value may use `{ENV:VAR}`. Reference: [Docs/08-cli-reference/command-reference.md](Docs/08-cli-reference/command-reference.md).

## Facts that are easy to get wrong

- **Version** lives in `Directory.Build.props` (`RayMigratorVersion`, always the next release) and must match the Licensed Work row in `LICENSE.md`. CI enforces it with `.github/scripts/check-license-version.sh`. Local builds are `X.Y.Z-dev+<sha>`.
- **Commands and options** are defined in `Raycoon.RayMigrator.Core/Configuration/Options/CommandLineConfiguration.cs`. The Console project is essentially `Program.cs`.
- **Configuration** sits under the `RayMigrator` root section: `Repository`, `ProductDefaults` → `TargetGroupDefaults` → `TargetDefaults`, `Products` → `TargetGroups` → `Targets`, `DatabaseLogging`, `CliTools`, `Serilog`. The target-group order option is `TargetMigrationOrder` (`FileByFile` / `TargetByTarget`); `TargetGroupMigrationOrder` on a product orders the groups. Arrays whose elements carry an `Alias` merge by alias across the `appsettings*.json` hierarchy (ADR-021).
- **Placeholders**: `{ENV:VAR}` in configuration and CLI values, `{CFG:SchemaName}` / `{CFG:TableBaseName}` in SQL templates. Runtime values are ADO parameters.
- **SQL templates** are `Content` files, not embedded resources. Result contract is `SELECT 'code,message'` with codes from `Shared/Constants/TemplateResultCode.cs`.
- **Options validation** uses DataAnnotations plus `Ray*Attribute`s in Core; enum options are strings resolved through `ParsedEnumOption<T>`. Cross-field rules live in the `Validation` project and are shared with the wizard.
- **Docs and repo**: `CLAUDE.md` is tracked. `.claude/` (skills, agents, settings) and `CLAUDE.local.md` are local and gitignored. `Docs/arc42/` is mirrored to the GitHub Wiki on every push to `main`.
- **Release** follows [Docs/11-development/release-process.md](Docs/11-development/release-process.md). Do not tag or push to `main` outside that process.

## Claude Code tooling (local, not in git)

Skills: `/build`, `/full-test`, `/review`, `/docker <all|sqlserver|postgresql|mariadb|mysql>`, `/deploy <vX.Y.Z>`, `/docs`, `/implement`, `/implement-wt`, `/logging`, `/dotnet-review`.
Agents: `architect`, `dev`, `code-review`, `code-audit`, `unit-test`, `integration-test`, `docs`, `migration-validator`, `test-coverage-optimizer`.

## Documentation map

Index: [Docs/README.md](Docs/README.md).

| Folder | Content |
|---|---|
| [01-architecture](Docs/01-architecture/) | Layers, patterns, DI, design decisions |
| [02-core-concepts](Docs/02-core-concepts/) | Migration context, state machine, hash validation, execution modes, error handling, resilience |
| [03-database-layer](Docs/03-database-layer/) | DAL architecture, template system, repository schema, SQL dialects |
| [04-service-layer](Docs/04-service-layer/) | Migration service, file discovery, block execution |
| [05-console-layer](Docs/05-console-layer/) | Command structure, launch profiles |
| [06-configuration-reference](Docs/06-configuration-reference/) | Every option, appsettings hierarchy, settings inheritance |
| [07-migration-files](Docs/07-migration-files/) | File naming, TOML header, rollback files, `migsettings` |
| [08-cli-reference](Docs/08-cli-reference/) | Commands and global options |
| [09-extending](Docs/09-extending/) | New command, new database type, external DAL development |
| [10-testing](Docs/10-testing/) | Docker infrastructure, engine tests, unit tests, coverage matrix |
| [11-development](Docs/11-development/) | Developer workflow, coding conventions, release process |
| [12-config-wizard](Docs/12-config-wizard/) | Wizard architecture, services, file hierarchy, validation |
| [arc42](Docs/arc42/) | arc42 architecture documentation and ADRs |
| [user-manual](Docs/user-manual/) | Tutorial-driven guide (BookStore example) |
| [appendix](Docs/appendix/) | Glossary, troubleshooting, open features, validation rules |
| [examples](Docs/examples/) | Working `appsettings*.json` and migration examples |

Test migration sets: `Testing/MigrationFiles/Tests_<Db>/`, `Tests_Success_<Db>/`, `Tests_SqlCmdDemo/`; SQLite sets in `Raycoon.RayMigrator.Tests.Engine/MigrationFiles/`. Legal and process files at the root: `LICENSE.md` (BUSL-1.1), `CONTRIBUTING.md`, `CLA.md`, `SECURITY.md`, `CHANGELOG.md`, `Docs/license-change-dates.md`.
