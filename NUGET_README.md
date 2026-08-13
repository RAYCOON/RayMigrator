# RayMigrator

Professional database migration framework that manages versioned schema migrations across multiple database engines.

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

## Contributing

We welcome bug reports and feature requests. See [CONTRIBUTING.md](https://github.com/RAYCOON/RayMigrator/blob/main/CONTRIBUTING.md) for details.

## License (TL;DR)

Licensed under **Business Source License 1.1 (BUSL-1.1)** with a custom
Additional Use Grant. Each version converts to **Apache License, Version 2.0**
four years after its release.

**This version is free for everyone, for any purpose.** The Additional Use
Grant places no conditions on production use — no organization-size threshold,
no restriction by legal form or sector, no internal-use requirement, and no
restriction on offering RayMigrator to third parties as a hosted, SaaS, or
managed service.

Non-production use (dev, test, evaluation, QA, research) is free under the BSL
grant itself.

You may copy, modify, create derivative works from, and redistribute the
source. Derivative works stay under this license and may not carry the
RayMigrator or RAYCOON marks.

> BUSL-1.1 applies separately to each version. The grant above ships with this
> version; every release carries its own `LICENSE.md`.

The `Raycoon.RayMigrator.Database.Example` skeleton is MIT-licensed so external
DAL plugin authors can copy it as a starting point.

## Full License

See `LICENSE.md` in the repository.

## Contact

raymigrator@raycoon.com
