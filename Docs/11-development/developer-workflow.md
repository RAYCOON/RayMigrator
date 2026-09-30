# Developer Workflow

How to build, test, run and publish RayMigrator from a working copy. Conventions for the code itself are in [coding-conventions.md](coding-conventions.md); the release procedure is in [release-process.md](release-process.md).

## Prerequisites

| Tool | Why |
|---|---|
| .NET SDK 10 | Libraries multi-target `net10.0;net9.0;net8.0`; the Web wizard and every test project are `net10.0` only. `global.json` pins `8.0.0` with `rollForward: latestMajor`, so any newer SDK is accepted, but building the whole solution needs SDK 10. |
| Docker Desktop (or Docker Engine + Compose v2) | Engine tests against SQL Server, PostgreSQL, MariaDB and MySQL. SQLite tests run without Docker. |
| PowerShell 7 (`pwsh`) | The `Testing/Docker/*.ps1` helper scripts. `docker compose` can be called directly instead. |
| JetBrains Rider or Visual Studio | Optional. Rider run configurations live in `.run/`, launch profiles in `Raycoon.RayMigrator.Console/Properties/launchSettings.json`. |

## Solution layout

Twenty-three projects in `RayMigrator.sln`. See the solution map in [../../CLAUDE.md](../../CLAUDE.md) and [../01-architecture/component-responsibilities.md](../01-architecture/component-responsibilities.md).

Key build facts:

- `Directory.Build.props` holds the version (`RayMigratorVersion`) and shared NuGet metadata. It sets no target framework and no analyzers.
- `Directory.Packages.props` enables central package management. Add package versions there, not in the csproj files.
- Every project has `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. There is no `TreatWarningsAsErrors`.
- Code style hints come from `.editorconfig` at the repository root. They are IDE suggestions and warnings, not build errors.
- The Console project selects an edition through the `BuildStage` MSBuild property:

| Build | Constant | Edition shown in `--startup-info` |
|---|---|---|
| Debug | `DEVELOPMENT_EDITION` | Development Edition |
| Release, `-p:BuildStage=Docker` | `PARTNER_EDITION` | Partner Edition |
| Release, `-p:BuildStage=Production` | `PRODUCTION_EDITION` | Professional Edition (CI releases) |

## Build

```bash
dotnet build                          # every project, every target framework
dotnet build --framework net10.0      # single framework, much faster for a check
dotnet build -c Release --framework net10.0
```

The `/build` skill wraps the second form and reports warnings and errors grouped by project.

## Run the CLI from source

The Console multi-targets, so `dotnet run` needs `--framework`:

```bash
dotnet run --project Raycoon.RayMigrator.Console --framework net10.0 -- migrate-up --product <Product> -env Docker --run-mode simulate
dotnet run --project Raycoon.RayMigrator.Console --framework net10.0 -- info -p <Product> -env Docker
```

Configuration files are read from the working directory or from `--config-dir`. The Console project ships `appsettings.json` plus one `appsettings.<Product>.Docker.json` per test product (`RM_Tests_Win_SqlServer`, `RM_Tests_Mac_PostgreSQL`, ...). The launch profiles set the environment variables those files reference; see [../05-console-layer/launch-profiles.md](../05-console-layer/launch-profiles.md).

## Tests

### Unit tests (no Docker)

```bash
dotnet test Raycoon.RayMigrator.Tests.Unit/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Unit.Validation/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web/ --framework net10.0
```

CI (`build-test.yml`) runs `Tests.Unit` on every push to `develop`. Run the other three locally when you touch Validation or the wizard.

### Engine tests (Docker)

```bash
dotnet test Raycoon.RayMigrator.Tests.Engine/ --framework net10.0
dotnet test Raycoon.RayMigrator.Tests.Engine/ --framework net10.0 --filter "Engine=PostgreSQL"
dotnet test Raycoon.RayMigrator.Tests.Engine/ --framework net10.0 --filter "Category=MigrateDown"
dotnet test Raycoon.RayMigrator.Tests.Engine/ --framework net10.0 --filter "FullyQualifiedName~RollbackTests&Engine=SqlServer"
```

Traits: `Engine` = `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite`; `Category` = `MigrateUp`, `MigrateDown`, `Compound`, `CliTool`, `CliToolDocker`, `Features`.

The engine tests need no environment variables. Connection strings are hard-coded in `Raycoon.RayMigrator.Tests.Engine/Fixtures/<Db>Fixture.cs` and match `Testing/Docker/default.env`. Each test starts with `Assert.SkipUnless(Fixture.IsDatabaseAvailable, ...)`, so an unreachable database produces skipped tests, not failures. SQLite tests use temporary files under `%TEMP%/RayMigrator_SqliteTests/`.

Details: [../10-testing/engine-tests.md](../10-testing/engine-tests.md).

### Docker test databases

| Container | Image | Host port | Login used by the fixtures |
|---|---|---|---|
| `rm_db_sqlserver` | SQL Server 2022 | 1433 | `sa` / `P@ssw0rd!` |
| `rm_db_postgresql` | PostgreSQL | 5432 | `postgres` / `postgres123` |
| `rm_db_mariadb` | MariaDB 11.6 | 3306 | `rayuser` / `raypass123` |
| `rm_db_mysql` | MySQL 8.4 | 3307 (container 3306) | `rayuser` / `raypass123` |

```bash
# start (from the repository root)
./Testing/Docker/RunDocker.default.all.ps1          # or RunDocker.default.<sqlserver|postgresql|mariadb|mysql>.ps1
# or, from Testing/Docker/
docker compose --env-file default.env --profile all up -d --build

# status
docker ps --filter "name=rm_db_"

# stop and remove volumes (from Testing/Docker/)
docker compose --env-file default.env --profile all down -v
./TeardownDocker.ps1                                # same thing
```

SQL Server needs about a minute after a fresh build before its health check passes. Every variable in `default.env` is mandatory; a custom `<name>.env` (gitignored) can be used with `RunDocker.<name>.<profile>.ps1` or `--env-file <name>.env`. The `/docker` skill wraps start, status and stop.

Details: [../10-testing/test-infrastructure.md](../10-testing/test-infrastructure.md).

### Full run

The `/full-test` skill builds, runs the unit tests, checks the containers and runs the engine tests for the databases that are up.

## Publish locally

Rider users have 13 run configurations in `.run/`:

- `Publish RayMigrator (<rid>)` for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`: Release, `net10.0`, single-file, self-extracting, output `_publish/<rid>/` (gitignored). They do not set `BuildStage`, so the result is neither Partner nor Professional edition.
- `Publish ConfigWizard`: Release, `net10.0`, no runtime identifier and no single-file (a Blazor WebAssembly app is the same static bundle for every platform; a RID plus `PublishSingleFile` fails with `NETSDK1098`), output `_publish/configwizard/`.
- `Publish All (<rid>)`: compound of the two above.

Rider identifies the project of a publish configuration by `uuid_high`/`uuid_low`, which are the two signed 64-bit halves of the project GUID in `RayMigrator.sln` (Console `AB25DF86-3E01-4642-A093-F5128B1FDB3B`, ConfigWizard.Web `FD34468E-97DA-4435-8B1B-E90BE2B9A047`). When you add or edit a configuration, check that the pair still matches the intended project.

The command CI uses for a release binary:

```bash
dotnet publish Raycoon.RayMigrator.Console/Raycoon.RayMigrator.Console.csproj -c Release -r win-x64 -f net10.0 -o publish/win-x64 \
  --no-self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true \
  -p:Version=<X.Y.Z> -p:BuildStage=Production
```

After publish, the Console project copies the DAL SQL templates into `DataAccessLayers/<Db>/` next to the binary, plus `Examples/`, `LICENSE.md`, `NOTICE.md` and `THIRD-PARTY-NOTICES.md`. The release workflow fails if any of these is missing.

## NuGet packages

Fifteen projects are packable, each with `PackageId` equal to the project name: `Core`, `Shared`, `Infrastructure`, `Services`, `Services.Abstractions`, `Pipeline`, `Database`, `Database.Common`, the five `Database.<Db>` plugins, `Validation` and `Testing`. `GeneratePackageOnBuild` is `false`; pack explicitly:

```bash
dotnet pack -c Release -p:Version=<X.Y.Z>
```

Not packable: `Console`, `ConfigWizard.*`, `Database.Example` and the test projects. Publishing to nuget.org is a manual workflow dispatch; see [release-process.md](release-process.md).

## Continuous integration

| Workflow | Trigger | Does |
|---|---|---|
| Build & Test (`build-test.yml`) | push to `develop`, tag `v*`, PR to `main` | `check-license-version.sh`, restore, Release build, `Tests.Unit` on `net10.0`. The green check on the commit is what branch protection on `main` requires. |
| Publish Release (`publish-release.yml`) | successful Build & Test of a `v*` tag | Publishes `win-x64` (zip), `osx-arm64` and `linux-x64` (tar.gz) with `BuildStage=Production`, creates the GitHub release with generated notes. |
| Deploy ConfigWizard Web (`deploy-configwizard.yml`) | successful Build & Test of a `v*` tag | `check-legal-pages.sh`, publishes the Blazor app to Azure Static Web Apps. |
| Publish NuGet (`publish-nuget.yml`) | manual dispatch | Packs and pushes every package with the version from `Directory.Build.props` (Trusted Publishing, repository variable `NUGET_USER`). |
| Sync arc42 Wiki (`sync-arc42-wiki.yml`) | push to `main` touching `Docs/arc42/**`, or manual | Mirrors `Docs/arc42/*.md` to the GitHub Wiki. |
| Claude Code (`claude.yml`), Claude Code Review (`claude-code-review.yml`) | `@claude` mentions; PR opened or updated | Runs `claude-code-action`, which reads this repository's `CLAUDE.md`. |

## Branches, commits, pull requests

- `develop` is the working branch. Feature branches start from it and PRs target it. `main` only receives a fast-forward from `develop` at release time; nothing is committed to `main` directly. See [../../CONTRIBUTING.md](../../CONTRIBUTING.md).
- Commit subjects are one imperative English sentence that says what changed and why it matters, without type prefixes (`feat:`, `fix:`) and without a trailing period. Issue references go in parentheses at the end: `Merge configuration arrays by alias in the engine and the Config Wizard (#23)`. Fixed forms: `Release X.Y.Z`, `Start X.Y.Z on develop`, `ADR-0NN: <title>`.
- No AI attribution anywhere: no `Co-Authored-By: Claude`, no `Generated with ...` trailers, no AI references in code or comments.
- Every behaviour change, fix or new option gets a line under `[Unreleased]` in `CHANGELOG.md` (Keep a Changelog format).
- The PR template asks for a summary, the related issue (`Fixes #123`), the list of changes, the test checklist and the CLA confirmation.

## Claude Code tooling

`CLAUDE.md` at the repository root is tracked and shared by every machine and by the GitHub workflows. The `.claude/` folder (skills, agents, settings) and `CLAUDE.local.md` are local and gitignored; the skill and agent names are listed in `CLAUDE.md` so that a fresh clone knows they exist.
