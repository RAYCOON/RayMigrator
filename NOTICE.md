# RayMigrator License Notice

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

RayMigrator is **source-available** under the Business Source License 1.1
(BUSL-1.1) with a custom Additional Use Grant. It is **not OSI-approved
open source** until the Change Date specified in `LICENSE.md`.

After the Change Date (4 years after each release), the version becomes
available under the Apache License, Version 2.0.

---

## Free usage

This version of RayMigrator is free of charge for everyone, for any purpose.

The Additional Use Grant in `LICENSE.md` places no conditions on production
use — no organization-size threshold, no restriction by legal form or sector,
no internal-use requirement, and no restriction on offering the Licensed Work
to third parties as a hosted, SaaS, or managed service.

Non-production use (development, testing, evaluation, QA, research) is free
under the BSL grant itself, independently of the Additional Use Grant.

Note that the Business Source License applies separately to each version of
the Licensed Work. The Additional Use Grant above is the one distributed with
this version; other versions carry their own `LICENSE.md`.

---

## Database.Example carve-out

The directory `Raycoon.RayMigrator.Database.Example` is licensed under the
**MIT License** so external developers can copy it as a starting point for
their own DAL plugin implementations. See
`Raycoon.RayMigrator.Database.Example/LICENSE.md` for the MIT terms.

The MIT carve-out applies **only** to that directory. The rest of the
RayMigrator repository is governed by `LICENSE.md` at the repository root.

---

## External DAL plugins

If you write your own DAL plugin that loads into a RayMigrator process:

- **Your plugin source code** is your property under your chosen license —
  RayMigrator's license does not extend to it.
- **Running your plugin in a RayMigrator process** is a use of the Licensed
  Work and is therefore governed by `LICENSE.md`. For this version that use
  is free of charge, whatever the size or nature of your organization.

---

## Trademarks

"RayMigrator" and "RAYCOON" — including all associated logos, word marks, and
visual identities — are claimed as unregistered trademarks of RAYCOON.com GmbH.
Nothing in BUSL-1.1 or the BSL grant of redistribution rights conveys any right
to use these trademarks.

Verbatim redistribution of unmodified copies under the original name and
identifiers is expressly permitted. **Modified or derivative** versions of
RayMigrator, by contrast, must not be distributed under the names
"RayMigrator" or "RAYCOON" and must adopt a distinct name not derived from
these Marks. Nominative reference (e.g., "compatible with RayMigrator") is
permitted only to the extent necessary for accurate technical description.

For the binding Trademark Reservation see the corresponding section in
`LICENSE.md`.

---

## Contact

Questions about licensing, support, or partnerships: `raymigrator@raycoon.com`

---

## Principle

Source-available, but not open source — yet.
