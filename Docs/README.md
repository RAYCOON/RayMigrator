# RayMigrator Documentation

This is the authoritative implementation reference for RayMigrator, a professional database migration framework for managing versioned database schema changes across SQL Server, PostgreSQL, MariaDB, MySQL, and SQLite.

## Quick Navigation

| I want to... | Go to... |
|--------------|----------|
| Understand the architecture | [01-architecture/overview.md](01-architecture/overview.md) |
| Learn core concepts | [02-core-concepts/migration-context.md](02-core-concepts/migration-context.md) |
| Configure the application | [06-configuration-reference/appsettings-hierarchy.md](06-configuration-reference/appsettings-hierarchy.md) |
| Understand all settings & inheritance | [06-configuration-reference/settings-inheritance-overview.md](06-configuration-reference/settings-inheritance-overview.md) |
| Create migration files | [07-migration-files/directory-structure.md](07-migration-files/directory-structure.md) |
| Use CLI commands | [08-cli-reference/migrate-up.md](08-cli-reference/migrate-up.md) |
| Add a new database type | [09-extending/new-database-type.md](09-extending/new-database-type.md) |
| Follow a step-by-step tutorial | [user-manual/README.md](user-manual/README.md) |
| Recover from a failed migration | [02-core-concepts/error-scenarios-and-recovery.md](02-core-concepts/error-scenarios-and-recovery.md) |
| Debug an issue | [appendix/troubleshooting.md](appendix/troubleshooting.md) |
| Use the interactive configuration wizard | [12-config-wizard/overview.md](12-config-wizard/overview.md) |

## Documentation Structure

### [01-architecture/](01-architecture/)
System design and architectural decisions. Start here to understand how RayMigrator works.

- [overview.md](01-architecture/overview.md) - 7-layer architecture with diagrams
- [design-decisions.md](01-architecture/design-decisions.md) - Why decisions were made
- [component-responsibilities.md](01-architecture/component-responsibilities.md) - Each layer's role
- [data-flow.md](01-architecture/data-flow.md) - Request to response flow
- [dependency-injection.md](01-architecture/dependency-injection.md) - DI container setup
- [patterns.md](01-architecture/patterns.md) - Repository, Template, Context, Options patterns

### [02-core-concepts/](02-core-concepts/)
Domain model and business logic fundamentals.

- [migration-context.md](02-core-concepts/migration-context.md) - MigrationContext lifecycle
- [migration-state-machine.md](02-core-concepts/migration-state-machine.md) - State transitions
- [configuration-system.md](02-core-concepts/configuration-system.md) - Options pattern hierarchy
- [hash-validation.md](02-core-concepts/hash-validation.md) - File/config/blocks hashing
- [execution-modes.md](02-core-concepts/execution-modes.md) - Operating modes, migration order (Simultaneously/Successively), and run modes (Validate/Simulate/Migrate)
- [error-handling.md](02-core-concepts/error-handling.md) - Error actions and rollback
- [error-scenarios-and-recovery.md](02-core-concepts/error-scenarios-and-recovery.md) - Error scenario matrix and recovery procedures
- [resilience.md](02-core-concepts/resilience.md) - Retry, recovery, orphaned run detection
- [concurrency-control.md](02-core-concepts/concurrency-control.md) - Exclusive run protection

### [03-database-layer/](03-database-layer/)
Database implementation details with ERD diagrams.

- [dal-architecture.md](03-database-layer/dal-architecture.md) - DAL interface design
- [template-system.md](03-database-layer/template-system.md) - SQL template placeholders
- [template-execution-order.md](03-database-layer/template-execution-order.md) - Template execution order on initial startup
- [repository-schema.md](03-database-layer/repository-schema.md) - 11 repository tables
- [logging-schema.md](03-database-layer/logging-schema.md) - Logging tables
- [sql-dialects.md](03-database-layer/sql-dialects.md) - Database-specific differences
- [adding-new-database.md](03-database-layer/adding-new-database.md) - Redirect → [09-extending/new-database-type.md](09-extending/new-database-type.md)

### [04-service-layer/](04-service-layer/)
Service implementation and business operations.

- [migration-service.md](04-service-layer/migration-service.md) - IMigrationService interface
- [activity-diagrams.md](04-service-layer/activity-diagrams.md) - Command flow diagrams (Migrate-Up, Migrate-Down, Baseline)
- [template-executor.md](04-service-layer/template-executor.md) - Template execution
- [file-discovery.md](04-service-layer/file-discovery.md) - Migration file scanning
- [block-execution.md](04-service-layer/block-execution.md) - SQL block parsing

### [05-console-layer/](05-console-layer/)
CLI implementation using System.CommandLine.

- [command-structure.md](05-console-layer/command-structure.md) - CLI setup
- [raymigrator-service.md](05-console-layer/raymigrator-service.md) - Service bridge
- [launch-profiles.md](05-console-layer/launch-profiles.md) - IDE integration
- [adding-new-command.md](05-console-layer/adding-new-command.md) - Redirect → [09-extending/new-command.md](09-extending/new-command.md)

### [06-configuration-reference/](06-configuration-reference/)
Complete configuration documentation.

- [appsettings-hierarchy.md](06-configuration-reference/appsettings-hierarchy.md) - File precedence
- [repository-options.md](06-configuration-reference/repository-options.md) - Repository settings
- [product-options.md](06-configuration-reference/product-options.md) - Product settings
- [target-group-options.md](06-configuration-reference/target-group-options.md) - Target group settings
- [target-options.md](06-configuration-reference/target-options.md) - Target settings
- [cli-tools-options.md](06-configuration-reference/cli-tools-options.md) - CLI tools for external migration execution
- [logging-options.md](06-configuration-reference/logging-options.md) - Logging configuration
- [environment-variables.md](06-configuration-reference/environment-variables.md) - {ENV:} placeholders
- [bootstrap-options.md](06-configuration-reference/bootstrap-options.md) - Bootstrap configuration (two-phase loading, Serilog initialization)
- [settings-inheritance-overview.md](06-configuration-reference/settings-inheritance-overview.md) - Complete settings map across all 4 layers with inheritance chains

### [07-migration-files/](07-migration-files/)
Migration file specification.

- [directory-structure.md](07-migration-files/directory-structure.md) - Folder hierarchy
- [file-naming.md](07-migration-files/file-naming.md) - Naming conventions
- [toml-metadata.md](07-migration-files/toml-metadata.md) - [RayMigrator] section
- [migsettings-files.md](07-migration-files/migsettings-files.md) - Control files
- [rollback-files.md](07-migration-files/rollback-files.md) - .rollback.sql convention
- [environment-specific.md](07-migration-files/environment-specific.md) - .{Environment}.sql files

### [08-cli-reference/](08-cli-reference/)
Command line reference. RayMigrator provides 7 commands: Migrate-Up, Migrate-Down, Validate-Hash, Update-Hash, Info, Baseline, and Fix.

- [migrate-up.md](08-cli-reference/migrate-up.md) - Apply migrations
- [migrate-down.md](08-cli-reference/migrate-down.md) - Rollback migrations
- [validate-hash.md](08-cli-reference/validate-hash.md) - Verify integrity
- [update-hash.md](08-cli-reference/update-hash.md) - Update hashes
- [global-options.md](08-cli-reference/global-options.md) - Common parameters
- [command-reference.md](08-cli-reference/command-reference.md) - Complete command and option matrix (all 7 commands including Info, Baseline, and Fix)

### [09-extending/](09-extending/)
Extension guides for developers.

- [new-database-type.md](09-extending/new-database-type.md) - Add database support
- [new-command.md](09-extending/new-command.md) - Add CLI command
- [template-customization.md](09-extending/template-customization.md) - Modify SQL templates
- [external-dal-development.md](09-extending/external-dal-development.md) - External DAL development guide

### [10-testing/](10-testing/)
Testing infrastructure and test documentation.

- [test-infrastructure.md](10-testing/test-infrastructure.md) - Docker setup and container configuration
- [engine-tests.md](10-testing/engine-tests.md) - Engine integration tests (~888 tests across 171 test class files, test matrix, ScenarioBuilder API)
- [unit-tests.md](10-testing/unit-tests.md) - Unit test structure and conventions
- [cli-test-coverage-matrix.md](10-testing/cli-test-coverage-matrix.md) - CLI command test coverage matrix across unit and engine tests

### [12-config-wizard/](12-config-wizard/)
Blazor WASM wizard and shared domain library for creating and editing RayMigrator configuration files.

- [README.md](12-config-wizard/README.md) - Index and quick reference
- [overview.md](12-config-wizard/overview.md) - Introduction, wizard flow, ecosystem overview
- [architecture.md](12-config-wizard/architecture.md) - Models, enums, WizardState, WizardPhase
- [services.md](12-config-wizard/services.md) - Service class reference (ConfigurationSerializer, ConfigurationValidator, etc.)
- [file-hierarchy.md](12-config-wizard/file-hierarchy.md) - 4-level appsettings hierarchy, merge semantics
- [validation.md](12-config-wizard/validation.md) - Validation rules per section and field

The shared domain library (`ConfigWizard.Core`) is consumed by the Blazor WASM standalone app (`ConfigWizard.Web`, MudBlazor 9, multilingual DE/EN, hub-and-spoke flow: Welcome → Hub → Detailed Config → Overview).

### [appendix/](appendix/)
Reference materials.

- [glossary.md](appendix/glossary.md) - Terms and definitions
- [open-features.md](appendix/open-features.md) - Open features
- [troubleshooting.md](appendix/troubleshooting.md) - Common issues
- [validation-rules.md](appendix/validation-rules.md) - Shared validation rule catalog (consumed by `Raycoon.RayMigrator.Validation`)

### [user-manual/](user-manual/)
Tutorial-driven end-user guide with BookStore example.

- [README.md](user-manual/README.md) - User manual index and navigation
- [01-introduction.md](user-manual/01-introduction.md) - What is RayMigrator?
- [02-quick-start.md](user-manual/02-quick-start.md) - First migration in 10 minutes
- [03-concepts.md](user-manual/03-concepts.md) - Product/TargetGroup/Target hierarchy
- [04-configuration.md](user-manual/04-configuration.md) - appsettings.json structure
- [05-migration-files.md](user-manual/05-migration-files.md) - TOML metadata, SQL conventions
- [06-cli-commands.md](user-manual/06-cli-commands.md) - CLI command reference
- [07-execution-modes.md](user-manual/07-execution-modes.md) - Validate, Simulate, Migrate
- [08-error-handling.md](user-manual/08-error-handling.md) - Error action strategies
- [09-rollback-guide.md](user-manual/09-rollback-guide.md) - Rollback procedures
- [10-advanced-features.md](user-manual/10-advanced-features.md) - TargetMigrationOrder, hash validation, baseline
- [11-operations-guide.md](user-manual/11-operations-guide.md) - Production deployment
- [12-reference.md](user-manual/12-reference.md) - Quick reference tables

### [examples/](examples/)
Working configuration and migration examples.

- [README.md](examples/README.md) - Index with descriptions of every example file
- [appsettings.minimal.json](examples/appsettings.minimal.json) - Minimal config
- [appsettings.complete.json](examples/appsettings.complete.json) - All options
- [appsettings.docker.json](examples/appsettings.docker.json) - Docker environment
- [appsettings.docker-cli.json](examples/appsettings.docker-cli.json) - Docker CLI tool execution across all four database engines
- [migration-examples/](examples/migration-examples/) - Sample migrations (rollback file pair, master-data insert, environment-specific file, multi-block migration, `migsettings.txt`)

### [todo/](todo/)
Internal audit notes and planning documents (work-in-progress, not part of the user-facing documentation).

- [dal-best-practices-audit.md](todo/dal-best-practices-audit.md) - DAL best-practices audit
- [dal-audit/](todo/dal-audit/) - Per-DAL audit working files

## How to Use This Documentation

### For New Developers
1. Start with [01-architecture/overview.md](01-architecture/overview.md)
2. Read [02-core-concepts/](02-core-concepts/) in order
3. Explore specific sections as needed

### For Feature Implementation
1. Check [03-database-layer/](03-database-layer/) and [04-service-layer/](04-service-layer/)
2. Follow guides in [09-extending/](09-extending/)
3. Reference [06-configuration-reference/](06-configuration-reference/)

### For Debugging
1. Check [appendix/troubleshooting.md](appendix/troubleshooting.md)
2. Review [02-core-concepts/migration-state-machine.md](02-core-concepts/migration-state-machine.md)
3. Consult [02-core-concepts/error-scenarios-and-recovery.md](02-core-concepts/error-scenarios-and-recovery.md) for specific recovery steps
4. Examine [03-database-layer/template-system.md](03-database-layer/template-system.md)

## Key Architectural Concepts

RayMigrator uses a **layered architecture** with the CLI as its presentation layer.

```
Console Layer                         CLI (System.CommandLine)
            |
Pipeline Layer                        Pipeline orchestration
            |
Service Layer                         Business logic orchestration
            |
Core Layer                            Domain models, context, options, enums
            |
Infrastructure Layer                  Cross-cutting concerns (logging, utilities)
            |
Database Layer                        Database-specific DAL implementations
            |
Shared Layer                          Common types, exceptions, DTOs
```

See [Architectural Patterns](01-architecture/patterns.md) for details on the DI, Options, Repository, Template, and Context patterns.

## Quick Start

For pre-built binaries, download from the [GitHub Releases](https://github.com/RAYCOON/RayMigrator/releases) page. To build from source:

```bash
# Build
dotnet build

# Apply migrations
raymigrator Migrate-Up -p RayMigratorTests -env Docker --run-mode Migrate

# Simulate (dry run)
raymigrator Migrate-Up -p RayMigratorTests -env Docker --run-mode Simulate

# Rollback
raymigrator Migrate-Down -p RayMigratorTests -env Docker --to-release "Release 1.0" --run-mode Migrate

# Validate hashes
raymigrator Validate-Hash -p RayMigratorTests -env Docker
```

## Related Files

- **CLAUDE.md** - Project guidance for Claude Code (architecture overview, build commands, development guidelines)
- **[license-change-dates.md](license-change-dates.md)** - Per-version Change Date register. BUSL-1.1 applies separately to each version; this file records when each version was first publicly distributed and when it converts to Apache 2.0. Must be updated on every release.
- **Examples/** - Two complete example migration products (`MySimpleApplication`, `MyComplexApplication`) with Docker infrastructure (SQL Server + PostgreSQL). See [Examples/README.md](../Examples/README.md).
- **Testing/MigrationFiles/Tests_SqlServer/** (and `Tests_PostgreSQL/`, `Tests_MariaDb/`, `Tests_MySql/`) - Example migrations (additional active test sets: `Tests_Success_*` for success-only scenarios and `Tests_SqlCmdDemo` for CLI tool execution)
- **Built-in DAL plugin projects** (`Database.SqlServer/`, `Database.PostgreSQL/`, `Database.MariaDb/`, `Database.MySql/`, `Database.Sqlite/`) — each contains SQL templates copied to `DataAccessLayers/` at build time
- **External DAL skeleton** (`Database.Example/`) — template project for developing external DAL plugins

## Version

This documentation is for RayMigrator v0.10.x
