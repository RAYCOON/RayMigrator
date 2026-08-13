# Changelog

All notable changes to RayMigrator are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
RayMigrator follows Semantic Versioning where applicable.

## [0.11.0] — Unreleased

### Licensing

- Switched from RayMigrator Dual License Agreement (RMLA) v1.0 to
  Business Source License 1.1 (BUSL-1.1) with a custom Additional Use Grant.
  Versions less than or equal to 0.10.3 remain under RMLA v1.0.
- **Unconditional Additional Use Grant.** Production use of this version is
  free of charge for everyone, for any purpose. No organization-size
  threshold, no restriction by legal form or sector, no internal-use
  requirement, and no restriction on offering the Licensed Work to third
  parties as a hosted, SaaS, or managed service. Non-production use is free
  under the BSL grant itself.
- BUSL-1.1 applies separately to each version; the Additional Use Grant
  above is the one distributed with this version. Future versions carry
  their own `LICENSE.md` and may grant different terms.
- Removed from the Additional Use Grant, all obsolete under the
  unconditional grant: the Eligible Licensees clause (§ 14 / § 13 BGB), the
  eligibility categories and thresholds, the internal-use and
  no-third-party-offering conditions, the Anti-Circumvention block, the
  Conflict Rule, the Doubt clause, and seven now-unused definitions.
- Consumers (§ 13 BGB) are no longer excluded from production use. Added a
  Supplemental Terms clause under which the statutory rules on liability and
  limitation periods apply to consumers in place of the liability cap and
  the shortened limitation period.
- Reworded the backup obligation (Supplemental Terms § Liability § 5) to
  refer declaratorily to § 254 BGB rather than fixing the legal consequence.
- Added Supplemental Terms (governing law, liability, termination details)
  to align the license with mandatory provisions of German law without
  modifying the BSL 1.1 License Text itself (BSL 1.1 Covenant 4).
- Tightened Trademark Reservation: redistributed or derivative versions
  must adopt a distinct name not derived from the "RayMigrator" /
  "RAYCOON" Marks.
- Change Date: four (4) years after each version's first publicly available
  distribution — per-version, see `LICENSE.md` Parameters. First
  publication dates are tracked in `Docs/license-change-dates.md`.
- Change License: Apache License, Version 2.0.
- The `Raycoon.RayMigrator.Database.Example` skeleton project is licensed
  permissively under the MIT License (see its own `LICENSE.md`) so external
  developers can copy it as a starting point for their own DAL plugins.
- The 0.11.0 license framework was prepared without external counsel
  review; associated legal risks are accepted by RAYCOON.com GmbH.
- Liability cap for non-consumer licensees set to a flat **EUR 10,000 per
  contract year** (see `LICENSE.md` Supplemental Terms § Liability § 4). The
  previous two-branch cap referencing fees paid under a commercial license was
  removed along with the paid tier.
- Trademark status: the names "RayMigrator" and "RAYCOON" are claimed
  as **unregistered** trademarks; no DPMA/EUIPO filing has been made
  or is currently planned.

### Documentation

- Added a pre-1.0 maturity notice to `README.md`, `NUGET_README.md`,
  `NOTICE.md`, and the CLI startup banner.
- `COMMERCIAL-LICENSE.md` reframed as a licensing overview: no commercial
  license is required for this version; RayMigrator Studio remains a
  separately licensed product.
- Added `Docs/license-change-dates.md` as the per-version Change Date
  register.

### Security

- Bumped `Microsoft.Data.Sqlite` from 10.0.5 to 10.0.11, resolving
  **CVE-2025-6965** (GHSA-2m69-gcr7-jv3q, CVSS 7.2 High). The 10.0.5 line
  pulled `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which bundles a SQLite build
  older than 3.50.2 — in those builds the number of aggregate terms can
  exceed the number of available columns, causing memory corruption.
  10.0.11 moves the SQLitePCLRaw chain to 2.1.12, above the affected range.
  `dotnet list package --vulnerable --include-transitive` now reports no
  vulnerable packages across the solution.

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
- Added `THIRD-PARTY-NOTICES.md`, shipped alongside them. Several bundled
  dependencies (Serilog, SQLitePCLRaw) are Apache-2.0 and require their
  notices to travel with the redistribution; `Microsoft.Data.SqlClient.SNI.runtime`
  carries Microsoft Software License Terms with their own distribution
  conditions. The release workflow fails if any of the three files is missing.
