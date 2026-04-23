# Adding New Command

> **This page has been consolidated.** The complete guide for adding a new CLI command is now maintained in a single location.

See **[Adding a New CLI Command](../09-extending/new-command.md)** for the full step-by-step guide, including:

- `MigrationCommand` enum value (Core)
- Request/Response type definitions (Services.Abstractions)
- Service interface and implementation (Services)
- `CommandLineConfiguration` command factory and handler setup (Core)
- `RayMigratorService` bridge method and switch case (Pipeline)
- Unit testing and documentation checklist

## Related Documentation

- [Command Structure](command-structure.md) - CLI overview
- [RayMigratorService](raymigrator-service.md) - Bridge pattern
- [Migration Service](../04-service-layer/migration-service.md) - Service layer
