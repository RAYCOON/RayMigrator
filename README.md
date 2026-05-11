# RayMigrator

Professional cross platform database migration framework for versioned and release-based schema migrations across multiple database engines.

## Features

- **Multi-Database Support** — SQL Server, PostgreSQL, MariaDB, MySQL, SQLite
- **Versioned Migrations** — Track schema changes across releases with file-based migration scripts
- **Rollback Support** — Automatic rollback on error with configurable strategies (Terminate, Rollback, RollbackRelease)
- **Hash Validation** — Detect unauthorized changes to executed migration files
- **Transaction Control** — Per-migration transaction configuration, respecting database-specific capabilities
- **Product-Specific Migrations** — Target different products and their respective environments and database targets with a single configuration
- **Execution Modes** — Run migrations simultaneously or successively across target groups
- **Resilience** — Retry logic, orphaned run detection, and recovery procedures

## Supported Databases

SQL Server, PostgreSQL, MariaDB, MySQL, SQLite


### System Requirements

- .NET 8.0, 9.0, or 10.0 SDK (pre-built releases require the .NET 10 runtime)
- 4 GB RAM minimum
- Windows 10+, Ubuntu 24.04+, or macOS 10.15+

## Contributing

We welcome bug reports and feature requests. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## License (TL;DR)

RayMigrator is licensed under the **Business Source License 1.1 (BUSL-1.1)**
with a custom Additional Use Grant. Each version automatically converts to
**Apache License, Version 2.0** four years after its release.

### Free production use if:
- You are an **entrepreneur** (§ 14 BGB) or a **legal entity** — the
  Additional Use Grant is not available to consumers (§ 13 BGB), AND
- Your organization (including affiliates) has **fewer than 20 persons**
  (counting employees and comparable contractors), OR
- You are a **governmental entity**, **public institution**, **academic
  institution**, or **non-profit**
- AND you use RayMigrator **only internally**
- AND you do **not provide it as a service** (SaaS / hosting / managed service)

Non-production use (development, testing, evaluation, QA, research) is free
for everyone — including consumers.

### Commercial license required if:
- Your organization has **20 or more persons** (employees + comparable
  contractors), OR
- You use RayMigrator in production **outside the Additional Use Grant scope**, OR
- You provide it **to third parties** (SaaS, hosting, services), OR
- You are a **consumer** (§ 13 BGB) and intend to use RayMigrator in production

### Important rule
> If there is any reasonable doubt whether the free tier applies, a commercial
> license is required.

### Database.Example carve-out
The `Raycoon.RayMigrator.Database.Example` skeleton is licensed under the
**MIT License** so you can freely copy it as a starting point for your own
DAL plugin without commercial-license obligations on the Example code.

---

Full license: see [LICENSE.md](LICENSE.md). Commercial licensing inquiries:
`raymigrator@raycoon.com`.