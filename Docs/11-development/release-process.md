# Release Process

Step-by-step checklist for publishing a RayMigrator version. It reflects what the release commits (`Release 0.15.0`, `Start 0.16.0 on develop`) and the GitHub workflows actually do. The branch model is described in [../../CONTRIBUTING.md](../../CONTRIBUTING.md); the licence register in [../license-change-dates.md](../license-change-dates.md).

## Versioning in one paragraph

`Directory.Build.props` carries `RayMigratorVersion`, which is always the version being worked towards. Local and `develop` builds get the suffix `-dev` plus the commit hash (`0.16.0-dev+b6638a1`). The release workflows pass `-p:Version=<tag>` and replace prefix and suffix. The version is bumped immediately after a release, not as part of it, together with the Licensed Work row in `LICENSE.md`. `.github/scripts/check-license-version.sh` fails the build when the two disagree.

## 0. Preconditions

- You are on `develop`, the working tree is clean, and the latest Build & Test run on `develop` is green.
- `RayMigratorVersion` in `Directory.Build.props` and the Licensed Work row in `LICENSE.md` already show the version you are releasing (they were set by the previous post-release bump).
- `CHANGELOG.md` has every user-visible change of this version under `[Unreleased]`.
- For the wizard deployment: the legal pages on raymigrator.com are live and the terms pages contain the `TermsVersion` string from `Raycoon.RayMigrator.ConfigWizard.Web/Services/TermsAcceptanceService.cs`. `.github/scripts/check-legal-pages.sh` verifies this and fails the deploy otherwise.

## 1. Release commit on `develop`

Edit, in one commit titled `Release X.Y.Z`:

| File | Change |
|---|---|
| `CHANGELOG.md` | Rename `## [Unreleased]` to `## [X.Y.Z] — YYYY-MM-DD` and add a fresh, empty `## [Unreleased]` above it. |
| `README.md` | Maturity notice heading and body, and the "Production use of RayMigrator X.Y.x" sentence (three places). |
| `NOTICE.md`, `NUGET_README.md` | Maturity notice (`X.Y.x`). |
| `SECURITY.md` | Supported version (`X.Y.x`). |
| `COMMERCIAL-LICENSE.md` | "Additional Use Grant shipped with RayMigrator X.Y.x". |
| `THIRD-PARTY-NOTICES.md` | "resolved for the RayMigrator X.Y.Z CLI" (full version). |
| `Raycoon.RayMigrator.ConfigWizard.Web/Services/LocalizationService.cs` | Key `Welcome.MaturityNotice`, English and German entries (`X.Y.x`). |
| `Docs/license-change-dates.md` | New register row and a note: first public distribution date (today), Change Date = date + 4 years, licence regime. |
| `Docs/README.md` | Version line at the bottom. |
| `Docs/arc42/01-Introduction-and-Goals.md`, `08-Crosscutting-Concepts.md`, `10-Quality-Requirements.md`, `11-Risks-and-Technical-Debt.md`, `12-Glossary.md`, `Docs/09-extending/new-database-type.md`, `external-dal-development.md` | Release-line statements (`X.Y.x`, `X.Y+1.0 in development`, `X.Y+1.0 on develop; X.Y.Z is the latest published package`). They already carry the post-release wording, because `main` and the wiki mirrored from it show the release commit. |

Then verify locally:

```bash
.github/scripts/check-license-version.sh . X.Y.Z
dotnet build -c Release --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Unit/ -c Release --framework net10.0
grep -rn "PREVIOUS.x\|PREVIOUS.0" README.md NOTICE.md NUGET_README.md SECURITY.md COMMERCIAL-LICENSE.md THIRD-PARTY-NOTICES.md   # expect no hits
```

Commit and push `develop`. Wait for Build & Test to pass on that commit.

## 2. Fast-forward `main`

```bash
git fetch origin
git checkout main
git merge --ff-only origin/develop
git push origin main
```

`--ff-only` must succeed. If it does not, `main` has diverged; stop and investigate, never merge or rebase onto `main`.

## 3. Tag

```bash
git tag vX.Y.Z            # on the Release commit, now the tip of main
git push origin vX.Y.Z
git checkout develop
```

The tag push starts Build & Test for the tag. On success, two workflows run automatically:

- **Publish Release** builds `win-x64`, `osx-arm64` and `linux-x64` with `-p:Version=X.Y.Z -p:BuildStage=Production`, checks that `LICENSE.md`, `NOTICE.md`, `THIRD-PARTY-NOTICES.md` and every `DataAccessLayers/<Db>/*.sql` are in the output, and creates the GitHub release `RayMigrator vX.Y.Z` with `RayMigrator-X.Y.Z-<rid>.zip|tar.gz` assets and generated notes.
- **Deploy ConfigWizard Web** runs the legal-pages check and deploys the Blazor app to Azure Static Web Apps.

## 4. Verify

- GitHub release exists, has three assets, and the notes look sane. Edit the notes if the generated list needs trimming.
- Download one archive and run `raymigrator --startup-info true info -p <any> -env <any>` or `raymigrator --version`; the reported version must be `X.Y.Z` with edition "Professional Edition".
- The Config Wizard shows the new version and maturity notice.

## 5. Publish NuGet (manual)

Dispatch the **Publish NuGet** workflow from `main` (or the tag). It reads `RayMigratorVersion` from `Directory.Build.props`, so it must run before the post-release bump lands on the ref you dispatch from. It needs the repository variable `NUGET_USER` and uses Trusted Publishing; pushes use `--skip-duplicate`.

Check nuget.org for the packages listed in [developer-workflow.md](developer-workflow.md#nuget-packages).

## 6. Post-release bump on `develop`

One commit titled `Start X.Y+1.0 on develop`:

| File | Change |
|---|---|
| `Directory.Build.props` | `<RayMigratorVersion>X.Y+1.0</RayMigratorVersion>` |
| `LICENSE.md` | Licensed Work row: `RayMigrator X.Y+1.0` |
| `Docs/README.md` | Version line: `X.Y+1.0-dev` |

Push `develop`. Build & Test runs `check-license-version.sh` and confirms the pair.

## 7. Close out

- Confirm the row in `Docs/license-change-dates.md` records the earliest actual public distribution (source push, GitHub release or NuGet listing, whichever came first) and the correct channel.
- If `Docs/arc42/` changed since the last release, the push to `main` has already synced the wiki; check the workflow run.
- Update the local `main` on other machines with `git pull --ff-only`.

## Quick reference

```text
develop:  Release X.Y.Z  ──push──▶  Build & Test green
main:     ff-only from develop  ──push──▶  tag vX.Y.Z  ──push──▶  Build & Test ──▶ Publish Release + Deploy ConfigWizard
manual:   Publish NuGet (from main)
develop:  Start X.Y+1.0 on develop
```

The local `/deploy` skill walks through this checklist interactively. Do not tag from a feature branch or from `develop`; the tag belongs on `main`.
