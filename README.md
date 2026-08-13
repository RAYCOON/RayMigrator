# RayMigrator

Professional cross platform database migration framework for versioned and release-based schema migrations across multiple database engines.

> **Maturity notice — 0.11.x**
>
> RayMigrator 0.11.x is a pre-1.0 release. Its behaviour has not yet been proven
> across a broad range of real-world production workloads.
>
> Database migrations are inherently irreversible: a failed or partially applied
> migration can cause data loss, schema corruption, or extended downtime.
>
> Deploy this version only in environments where a failed migration would not have
> far-reaching consequences for your organization or your projects — and only with
> a verified, restorable backup of every affected database taken immediately before
> each run.

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

### This version is free — with no conditions attached

Production use of RayMigrator 0.11.x costs nothing, for anyone, for any
purpose. The Additional Use Grant sets no organization-size threshold, no
restriction by legal form or sector, no internal-use requirement, and no
restriction on offering RayMigrator to third parties as a hosted, SaaS, or
managed service.

Non-production use — development, testing, evaluation, QA, research — is free
under the BSL grant itself.

You may also copy, modify, create derivative works from, and redistribute the
source. Derivative works remain under this license and may not carry the
RayMigrator or RAYCOON marks — see the Trademark Reservation in
[LICENSE.md](LICENSE.md).

### One thing worth knowing

BUSL-1.1 applies separately to each version. The grant above is the one shipped
with this version; every release carries its own `LICENSE.md`.

### Database.Example carve-out

The `Raycoon.RayMigrator.Database.Example` skeleton is licensed under the
**MIT License** so you can freely copy it as a starting point for your own
DAL plugin.

---

Full license: see [LICENSE.md](LICENSE.md). Questions about licensing, support,
or partnerships: `raymigrator@raycoon.com`.