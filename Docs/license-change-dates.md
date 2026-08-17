# License Change Date Register

Business Source License 1.1 applies **separately to each version** of the Licensed
Work, and each version converts to its Change License four years after its own
first publicly available distribution. This file is the authoritative record of
those dates.

Without it, there is no way to establish — four years from now — when a given
version became available under Apache License 2.0. The `LICENSE.md` shipped with
each version states the rule; this register states the resulting dates.

## Register

| Version | License regime | First public distribution | Change Date | Change License |
|---------|----------------|---------------------------|-------------|----------------|
| 0.11.0  | BUSL-1.1 + Additional Use Grant | **2026-08-17** (source; repository made public) | **2030-08-17** | Apache License 2.0 |
| ≤ 0.10.3 | RayMigrator Dual License Agreement (RMLA) v1.0 | *never publicly distributed* | — (no BSL Change Date) | — |

### Notes on the entries

**0.11.0** — the first version published under BUSL-1.1. Its Additional Use Grant
permits production use free of charge, without conditions. First public
distribution was the **source**, when the GitHub repository was made public on
2026-08-17; the GitHub binary release (tag `v0.11.0`) and the NuGet listing
followed the same day. The register records the earliest channel.

**≤ 0.10.3** — `v0.10.3` was tagged in the repository on 2026-04-23 but was not
publicly distributed. These versions carry RMLA v1.0, which has no Change Date
construction. They are listed only to make the licence history complete.

## What counts as "first publicly available distribution"

The earliest of:

- the GitHub Release for the version tag becoming publicly downloadable,
- the NuGet packages for the version becoming listed on nuget.org, or
- any other public distribution of the version's binaries or source.

Where these differ, record the earliest date and note which channel it was.

## Procedure on each release

1. Bump `RayMigratorVersion` in `Directory.Build.props`.
2. Update the **Licensed Work** row in `LICENSE.md` to the same version.
   `.github/scripts/check-license-version.sh` enforces that these two agree and
   runs in both `build-test.yml` and `publish-release.yml`.
3. Publish the release.
4. Add a row here with the actual publication date and the resulting Change Date
   (publication date + 4 years).
5. If the Additional Use Grant differs from the previous version, say so in the
   *License regime* column — that column is what tells a later reader which terms
   a given version was distributed under.

## Related

- [`LICENSE.md`](../LICENSE.md) — binding licence text and Parameters block
- [`COMMERCIAL-LICENSE.md`](../COMMERCIAL-LICENSE.md) — licensing overview
- [`NOTICE.md`](../NOTICE.md) — distribution notice
