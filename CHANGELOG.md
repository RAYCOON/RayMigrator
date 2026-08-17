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
  limitation periods apply to consumers in place of the liability cap, the
  backup obligation and the shortened limitation period (§ Liability
  §§ 4, 5 and 6).
- Reworded the backup obligation (Supplemental Terms § Liability § 5) to
  refer declaratorily to § 254 BGB rather than fixing the legal consequence.
- Added Supplemental Terms (governing law, liability, termination details)
  to align the license with mandatory provisions of German law without
  modifying the BSL 1.1 License Text itself (BSL 1.1 Covenant 4).
- Tightened Trademark Reservation: **modified or derivative** versions must
  adopt a distinct name not derived from the "RayMigrator" / "RAYCOON" Marks.
  Verbatim redistribution of unmodified copies under the original name,
  package identifiers, binary names, configuration keys, and CLI command
  names is expressly permitted — the reservation does not restrict the
  redistribution right the BSL grants.
- Moved the Definitions, the Database.Example carve-out and the Trademark
  Reservation out of the Additional Use Grant into the Supplemental Terms.
  The Additional Use Grant now consists of the grant itself and nothing
  else, so it cannot be read as imposing an additional restriction on the
  rights granted by BSL 1.1 (BSL 1.1 Covenant 2(a)).
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
  calendar year** (see `LICENSE.md` Supplemental Terms § Liability § 4). The
  previous two-branch cap referencing fees paid under a commercial license was
  removed along with the paid tier. The reference period is the calendar year
  rather than a "contract year", because the grant is perpetual, unconditional
  and free of charge and therefore has no contract term to count from.
- Trademark status: the names "RayMigrator" and "RAYCOON" are claimed
  as **unregistered** trademarks; no DPMA/EUIPO filing has been made.
- Added a **Precedence over Accompanying Materials** clause to the
  Supplemental Terms: `LICENSE.md` is the sole statement of the licensing
  terms, and statements in the website, README files, package metadata,
  changelogs, documentation or marketing are informative only and are not
  guarantees. This replaces the Conflict Rule that was removed with the
  conditional Additional Use Grant and closes the gap it left.
- Added a **Contractual Penalty** clause to the Supplemental Terms, covering
  breaches of the Trademark Reservation and of the BSL notice obligation.
  The amount is set by the Licensor at reasonable discretion under § 315 BGB
  and is reviewable by the court, rather than being a flat figure — a single
  flat penalty applied to breaches of differing weight is unenforceable in
  German standard business terms. The clause does not apply to consumers.

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

### Dependencies

- Bumped `Raycoon.Serilog.Sinks.SQLite` from 1.2.1 to 1.2.2. The release only
  raises the sink's own dependency floors (`Microsoft.Data.Sqlite` >= 10.0.11
  for the CVE above, `Serilog` >= 4.4.0); no API or behaviour changes.
- Bumped `Serilog` from 4.3.1 to 4.4.0, required by the sink above. A lower
  direct pin fails restore with NU1605 (package downgrade). This also raises
  the floor declared by the published `Raycoon.RayMigrator.Core` and
  `Raycoon.RayMigrator.Infrastructure` packages: consumers that pin Serilog
  4.3.x directly must move to 4.4.0 as well. `Database.Common` and `Shared`
  carry no Serilog dependency, so external DAL plugins are unaffected.
- Bumped the 13 `Microsoft.Extensions.*` packages from 10.0.5 to 10.0.11
  (.NET 10 servicing). This raises the floor declared by every published
  RayMigrator package that carries one of them; consumers pinning 10.0.5
  directly must move up as well. `Serilog.Extensions.Hosting` only requires
  >= 10.0.0, so no downgrade conflict arises.
- Bumped `Microsoft.Data.SqlClient` from 7.0.0 to 7.0.2. Fixes a
  `NullReferenceException` in `SqlCommand.Cancel()` when the connection has
  already been torn down — reachable from command timeouts. 7.0.2 also
  aligns the companion packages onto a single version, so the transitive
  `Microsoft.Data.SqlClient.Extensions.Abstractions` and
  `Microsoft.Data.SqlClient.Internal.Logging` move from 1.0.0 to 7.0.2. The
  accompanying `AssemblyVersion` change on those two is .NET Framework only
  and does not affect RayMigrator.
- Bumped `Npgsql` from 10.0.2 to 10.0.3 and `Serilog.Settings.Configuration`
  from 10.0.0 to 10.0.1; both are routine servicing releases.
- Bumped `Microsoft.AspNetCore.Components.WebAssembly` and its `DevServer`
  counterpart from 10.0.5 to 10.0.11. Config Wizard only; the CLI and the
  Engine packages are unaffected. Building the wizard now expects an SDK
  from the 10.0.400 band (runtime 10.0.11) so the WASM runtime pack matches.
- Bumped `System.CommandLine` from 2.0.5 to 2.0.11 (six servicing releases).
  Parse edge cases, option-parsing consistency, error-message wording and
  help formatting changed; the API additions (`HelpAction.MaxWidth`,
  `SetAction(Task<int>)`, `ArgumentResult.Implicit`) are unused so far. The
  one documented migration item for this range — custom help and version
  actions must set `ClearsParseErrors` — was already satisfied by
  `LogoHelpAction`, so no source change was required.
- Bumped `MySqlConnector` from 2.5.0 to 2.6.2, affecting both the MariaDb
  and the MySql DAL. Two behaviour changes are worth knowing about even
  though neither reaches RayMigrator's own schema, which uses `TIMESTAMP`
  exclusively: `BINARY` columns are no longer auto-detected as `Guid`, and
  negative `DATETIME` ticks are now rejected. Migration authors relying on
  either behaviour in their own scripts should re-check them. Parameter
  parsing and argument validation are also stricter, and
  `EnableResultSetHeaderEvent` is now opt-in.
- Bumped `MudBlazor` from 9.2.0 to 9.8.0 (six minor releases, four of them
  carrying breaking changes). Config Wizard only. Of the changes that touch
  components the wizard uses, only two can manifest: `MudNumericField` no
  longer steps on mouse wheel (the spinner arrows and keyboard still work),
  and `MudAppBar` no longer compensates for the scrollbar during a dialog's
  scroll lock, so content anchored to the right of the app bar shifts by the
  scrollbar width while a dialog is open — in the wizard that is the language
  selector, measured at 11 px. Cosmetic and self-reverting on close. The
  remaining breaking changes are unreachable here: no `Class` is passed to
  any `MudSwitch` or to the one adorned `MudTextField`, and `OnPaste` is
  unused.
- Bumped `Microsoft.NET.Test.Sdk` from 18.3.0 to 18.9.0 and
  `coverlet.collector` from 8.0.1 to 10.0.1. Test infrastructure only,
  redistributed in nothing. Coverlet 10 declares no breaking changes, adds
  .NET 10 support, and fixes an 8.0.1 defect that could produce an empty
  coverage report without failing; a collection run now yields 5566 of
  11624 lines across 18 assemblies. Coverlet skipped its 9.x line, so the
  two-major jump is smaller than it looks.
- Bumped `NSubstitute` from 5.3.0 to 6.2.0. Test infrastructure only. The
  breaking changes are a target-framework floor of .NET 8 / netstandard2.0,
  removal of legacy obsolete APIs, and nullable annotations on the public
  API. None of them bite: all three consuming test projects target
  `net10.0`, no legacy or `CompatArg` API is used, and the build stays at
  zero warnings, so the nullable annotations required no source change.

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
