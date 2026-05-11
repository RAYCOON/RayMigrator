# Changelog

All notable changes to RayMigrator are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
RayMigrator follows Semantic Versioning where applicable.

## [0.11.0] — <RELEASE_DATE>

### Licensing

- Switched from RayMigrator Dual License Agreement (RMLA) v1.0 to
  Business Source License 1.1 (BUSL-1.1) with a custom Additional Use Grant
  that mirrors the prior free-use conditions (organizations with fewer than
  20 employees including affiliates; governmental, public, academic, and
  non-profit entities; non-production use for everyone).
- Change Date: <RELEASE_DATE + 4 years> (per-release; each version's
  4-year clock starts at its release date).
- Change License: Apache License, Version 2.0.
- Versions less than or equal to 0.10.3 remain under RMLA v1.0.
- The `Raycoon.RayMigrator.Database.Example` skeleton project is licensed
  permissively under the MIT License (see its own `LICENSE.md`) so external
  developers can copy it as a starting point for their own DAL plugins
  without commercial-license obligations on the Example code itself.

### NuGet

- Package version bumped to 0.11.0.
- Added `busl-1.1`, `source-available`, `database-migration`, `dotnet`
  package tags for license-aware searches on nuget.org.
- Added explicit `<IsPackable>true</IsPackable>` on packageable projects
  (defensive against future SDK default changes).

### Distribution

- Self-contained and framework-dependent publish output now includes
  `LICENSE.md` and `NOTICE.md` next to the binary, satisfying BUSL-1.1
  Section 3 (Notices) requirements.
