# Adding New Database Support

> **This page has been consolidated.** The complete guide for adding a new database type is now maintained in a single location.

See **[Adding a New Database Type](../09-extending/new-database-type.md)** for the full step-by-step guide, including:

- Quick start using the `Database.Example` skeleton project (19 placeholder templates: 18 required by the engine + `Repository_MigrationRecordHistory_Archive.sql`)
- DAL class implementation with Oracle example
- Plugin architecture: each DAL is a separate project/assembly
- `[DatabaseType]` attribute as the runtime lookup key
- DAL classes must be `public` for cross-assembly `Activator.CreateInstance` by `DalFactory`
- Filesystem-based auto-discovery: `DalFactory` scans `DataAccessLayers/` subdirectories for DLLs
- All 18 SQL templates required by the engine, with placeholder and result conventions
- `RetryHelper` integration with custom transient error predicate
- Post-build target for copying DAL DLLs to `DataAccessLayers/{Type}/`
- Monorepo deployment (project references) and external plugin deployment (NuGet packages)
- Testing checklist

For developing a DAL **outside the monorepo**, see **[External DAL Development](../09-extending/external-dal-development.md)**, which covers NuGet package references to `Database.Common` and `Shared`, build output configuration, and deployment to a RayMigrator installation.

## Related Documentation

- [DAL Architecture](dal-architecture.md) - Plugin architecture, interface details, factory resolution
- [Template System](template-system.md) - Template guidelines
- [SQL Dialects](sql-dialects.md) - Existing dialects
- [Repository Schema](repository-schema.md) - Table structures
- [External DAL Development](../09-extending/external-dal-development.md) - External plugin development
