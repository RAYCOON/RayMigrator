# RayMigrator License Notice

RayMigrator is **source-available** under the Business Source License 1.1
(BUSL-1.1) with a custom Additional Use Grant. It is **not OSI-approved
open source** until the Change Date specified in `LICENSE.md`.

After the Change Date (4 years after each release), the version becomes
available under the Apache License, Version 2.0.

---

## Free usage

You may use RayMigrator in production at no cost if **all** of the following
apply:

- You qualify under the Additional Use Grant in `LICENSE.md`:
  - Your organization (including affiliates) employs **fewer than 20** people, OR
  - You are a Governmental Entity, 100% publicly owned company, Academic
    Institution, or Non-Profit organization
- You use it **only for internal operations**
- You do **not** make it available to third parties (no SaaS, no hosting,
  no managed services)

Non-production use (development, testing, evaluation, QA, research) is free
for everyone.

---

## Commercial license required

A commercial license is required if any of the conditions for free production
use are not met. Common triggers:

- 20 or more employees (including affiliates)
- Production use outside the Additional Use Grant scope
- Providing the software to third parties (SaaS, hosting, managed services)

Contact `raymigrator@raycoon.com` for commercial licensing terms.

---

## Database.Example carve-out

The directory `Raycoon.RayMigrator.Database.Example` is licensed under the
**MIT License** so external developers can copy it as a starting point for
their own DAL plugin implementations without commercial-license obligations
on the Example code itself. See `Raycoon.RayMigrator.Database.Example/LICENSE.md`
for the MIT terms.

The MIT carve-out applies **only** to that directory. The rest of the
RayMigrator repository is governed by `LICENSE.md` at the repository root.

---

## External DAL plugins

If you write your own DAL plugin that loads into a RayMigrator process:

- **Your plugin source code** is your property under your chosen license —
  RayMigrator's license does not extend to it.
- **Running your plugin in a RayMigrator process** is a use of the Licensed
  Work and must therefore comply with the BUSL-1.1 + Additional Use Grant
  conditions in `LICENSE.md` (or a commercial license).

In short: an organization that wouldn't qualify for the free tier still needs
a commercial RayMigrator license, even when their DAL plugin is their own.

---

## Trademarks

"RayMigrator" and "RAYCOON" are trademarks of RAYCOON.com GmbH, registered or
pending with the relevant trademark offices. Nothing in BUSL-1.1 grants any
right to use these trademarks, except as required to comply with attribution
obligations under the License.

---

## Principle

Source-available, but not open source — yet.
