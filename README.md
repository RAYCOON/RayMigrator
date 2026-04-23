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

### ✅ Free to use if:
- Your organization has **fewer than 20 employees**, OR
- You are a **governmental entity**, **public institution**, **academic institution**, or **non-profit**
- AND you use RayMigrator **only internally**
- AND you do **not provide it as a service** (SaaS / hosting / managed service)

---

### 💼 Commercial license required if:
- Your organization has **20 or more employees**, OR
- You use RayMigrator **in production outside the free scope**, OR
- You provide it **to third parties** (e.g. SaaS, hosting, services)

---

### 🔒 Important rule
> If there is any doubt whether free usage applies, a commercial license is required.

---

📄 Full license: see [LICENSE.md](LICENSE.md)  
📧 Contact: raymigrator@raycoon.com
For commercial licensing inquiries, contact raymigrator@raycoon.com.