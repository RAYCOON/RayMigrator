# arc42 Architecture Documentation - Authoring Guide

This folder is the single source of truth for the RayMigrator arc42 architecture
documentation. It is mirrored to the GitHub Wiki
(https://github.com/RAYCOON/RayMigrator/wiki) by the workflow
`.github/workflows/sync-arc42-wiki.yml`. Never edit pages in the wiki directly;
the next sync overwrites them.

This README is an authoring guide and is **not** copied to the wiki.

## Files and page names

| File | Wiki page |
|------|-----------|
| `Home.md` | Landing page, chapter overview |
| `_Sidebar.md` | Navigation shown on every wiki page |
| `_Footer.md` | Footer shown on every wiki page |
| `01-Introduction-and-Goals.md` | 01 Introduction and Goals |
| `02-Architecture-Constraints.md` | 02 Architecture Constraints |
| `03-Context-and-Scope.md` | 03 Context and Scope |
| `04-Solution-Strategy.md` | 04 Solution Strategy |
| `05-Building-Block-View.md` | 05 Building Block View |
| `06-Runtime-View.md` | 06 Runtime View |
| `07-Deployment-View.md` | 07 Deployment View |
| `08-Crosscutting-Concepts.md` | 08 Crosscutting Concepts |
| `09-Architecture-Decisions.md` | 09 Architecture Decisions |
| `10-Quality-Requirements.md` | 10 Quality Requirements |
| `11-Risks-and-Technical-Debt.md` | 11 Risks and Technical Debt |
| `12-Glossary.md` | 12 Glossary |

The wiki has a flat namespace. Do not create subfolders. The file name (without
`.md`) is the page name; GitHub renders hyphens as spaces in the page title.

## Link conventions

- **Between arc42 pages:** relative link to the file, e.g.
  `[Building Block View](05-Building-Block-View.md)` or with anchor
  `[DAL plugins](05-Building-Block-View.md#526-database-layer)`.
  The sync script strips `.md` so the link resolves inside the wiki as well.
- **To the detailed documentation under `Docs/` or to source code:** absolute
  URL on the `main` branch, e.g.
  `https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/overview.md`.
  Relative paths outside this folder do not resolve in the wiki.
- **Images:** avoid binary images. Use Mermaid fenced code blocks; the wiki
  renders them natively.

## Writing conventions

- Language: English. Spelling: US English.
- Audience: developers and architects who evaluate, integrate or extend
  RayMigrator. Assume familiarity with .NET and relational databases.
- Each chapter follows the arc42 template (https://arc42.org/overview) and starts
  with a one-paragraph statement of what the chapter answers.
- Be concrete. Name the actual project, class, interface, configuration key or
  CLI option. Verify every such name against the source tree before writing it.
- Do not duplicate the deep reference docs. Summarize, then link to the
  authoritative page in `Docs/`.
- Prefer tables and Mermaid diagrams over long prose. Keep a chapter between
  roughly 100 and 400 lines.
- Use `code formatting` for identifiers, file names, keys and commands.
- ASCII only in prose (no em dashes, no typographic quotes).
- Do not mention chapter authorship, generation dates or tooling inside pages.

## Terminology (use exactly these forms in prose)

| Term | Meaning |
|------|---------|
| RayMigrator | The product. Never "Ray Migrator" or "raymigrator" in prose. |
| repository (database) | The RayMigrator bookkeeping database that stores migration state, hashes and logs. Not a Git repository. Use "migration repository" on first use in a chapter. |
| product | A configured software product whose databases are migrated. Configuration node `Products`. |
| target group | A named group of database targets inside a product. Configuration node `TargetGroups`. |
| target | One concrete database (connection) inside a target group. Configuration node `Targets`. |
| migration file | A versioned SQL file with optional TOML metadata header, discovered from the migrations directory. |
| rollback file | The rollback counterpart of a migration file (see `Docs/07-migration-files/rollback-files.md` for the exact naming). |
| block | A unit of SQL inside a migration file executed as one statement batch. |
| DAL / DAL plugin | Database access layer implementation for one engine (`Raycoon.RayMigrator.Database.*`). |
| template | SQL template used to create and maintain the repository schema per engine. |
| execution mode | Operating mode, migration order and run mode. Check `Docs/02-core-concepts/execution-modes.md` for the exact enum names before writing. |
| Config Wizard | The web application `Raycoon.RayMigrator.ConfigWizard.Web` that generates configuration files. |
| engine | A database engine: SQL Server, PostgreSQL, MariaDB, MySQL, SQLite (always this order, these spellings). |

## Chapter scope map

| Chapter | Primary sources under `Docs/` and the tree |
|---------|--------------------------------------------|
| 01 | `README.md`, `user-manual/01-introduction.md`, `CHANGELOG.md` |
| 02 | `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `LICENSE.md`, `COMMERCIAL-LICENSE.md`, `CONTRIBUTING.md`, `.github/workflows/` |
| 03 | `01-architecture/overview.md`, `06-configuration-reference/cli-tools-options.md`, `08-cli-reference/` |
| 04 | `01-architecture/design-decisions.md`, `01-architecture/patterns.md` |
| 05 | `01-architecture/overview.md`, `component-responsibilities.md`, `dependency-injection.md`, `03-database-layer/dal-architecture.md`, `12-config-wizard/architecture.md`, `RayMigrator.sln` |
| 06 | `01-architecture/data-flow.md`, `04-service-layer/activity-diagrams.md`, `02-core-concepts/migration-state-machine.md`, `03-database-layer/template-execution-order.md` |
| 07 | `.github/workflows/publish-*.yml`, `deploy-configwizard.yml`, `NUGET_README.md`, `05-console-layer/launch-profiles.md`, `10-testing/engine-tests.md` |
| 08 | `02-core-concepts/*`, `03-database-layer/logging-schema.md`, `06-configuration-reference/settings-inheritance-overview.md`, `appendix/validation-rules.md` |
| 09 | `01-architecture/design-decisions.md`, `CHANGELOG.md` |
| 10 | `README.md` features, `10-testing/*`, `02-core-concepts/resilience.md` |
| 11 | `appendix/open-features.md`, `todo/`, `README.md` maturity notice, `SECURITY.md` |
| 12 | `appendix/glossary.md` |

## Sync workflow

- Trigger: push to `main` touching `Docs/arc42/**`, or manual `workflow_dispatch`.
- Script: `.github/scripts/sync-arc42-wiki.sh <source-dir> <wiki-checkout>`.
  It clears all top-level `*.md` pages in the wiki checkout, copies every `*.md`
  from this folder except `README.md`, rewrites `](NN-Name.md` links to
  `](NN-Name`, and leaves the commit to the workflow.
- Local dry run:

```bash
git clone https://github.com/RAYCOON/RayMigrator.wiki.git /tmp/wiki
.github/scripts/sync-arc42-wiki.sh Docs/arc42 /tmp/wiki
git -C /tmp/wiki status
```
