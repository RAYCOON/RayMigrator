# RayMigrator workshop

An interactive, 60-minute introduction to RayMigrator for developers and operators: a Marp slide deck, four hands-on exercises against SQL Server, model solutions, a cheat sheet and bonus exercises. The material targets RayMigrator **0.15.0** and was verified end-to-end against SQL Server 2022 with the 0.15.0 release binaries.

Audience: teams evaluating or introducing RayMigrator, and colleagues or partners being onboarded. No prior RayMigrator knowledge is needed; participants should be comfortable with SQL and a terminal.

## Contents

| Path | What it is |
|---|---|
| [slides/raymigrator-workshop.md](slides/raymigrator-workshop.md) | The deck, 55 slides, speaker notes in HTML comments |
| [slides/themes/raycoon.css](slides/themes/raycoon.css) | Marp theme (Inter, JetBrains Mono, RayMigrator colours) |
| [slides/assets/](slides/assets/) | Diagrams as SVG with their Mermaid sources (`.mmd`) |
| [exercises/00-setup/](exercises/00-setup/README.md) | Pre-session setup guide, `create-database.sql`, `reset-database.sql` |
| [exercises/bookstore/](exercises/bookstore/) | The participants' working folder: `appsettings.json`, the config template, an empty `Migrations/` tree |
| [exercises/01-first-migration/](exercises/01-first-migration/README.md) … [04-error-handling/](exercises/04-error-handling/README.md) | One README per exercise with steps, expected output and checks; `files/` holds copy-ready migrations |
| [exercises/05-bonus/](exercises/05-bonus/README.md) | Take-home tasks: secrets, a PostgreSQL target group, CLI tools |
| [exercises/solutions/](exercises/solutions/README.md) | The state of the working folder after each exercise, for catching up |
| [cheatsheet.md](cheatsheet.md) | One page: commands, run modes, TOML keys, configuration chain, exit codes |

## Storyline

One product, `BookStore`, grows through three releases on a SQL Server database: a `Books` table (exercise 1), authors with a foreign key, environment-specific seed data and rollback files (exercise 2), a changed file caught by `validate-hash` (exercise 3), and a release that fails on purpose, handled first with `Terminate`, then with `Rollback`, then repaired, plus a `RunAlways` view (exercise 4). The repository tables live in schema `ray` of the same database so that every participant creates a single database.

## Session plan (60 minutes)

| Time | Part | Hands-on |
|---|---|---|
| 0:00 | 0 Why: the problem, what RayMigrator is and is not | show of hands |
| 0:04 | 1 First migration: vocabulary, folders, configuration, `migrate-up`, repository, `info` | Exercise 1, 7 min |
| 0:17 | 2 Real migrations: TOML header, Validate → Simulate → Migrate, blocks, rollback files, `migrate-down`, environments, `RunAlways` | Exercise 2, 8 min |
| 0:30 | 3 Integrity and lifecycle: hashes, `validate-hash`, `update-hash`, status lifecycle, `baseline` | Exercise 3, 5 min |
| 0:39 | 4 When things go wrong: failing blocks, two execution paths, `MigrationErrorAction`, locks, `fix` | Exercise 4, 10 min |
| 0:54 | 5 Operating at scale: configuration hierarchy, secrets, Config Wizard, several targets, logging, pipeline | |
| 0:58 | 6 Wrap-up: Studio outlook, licence and maturity, links | Q&A |

**45-minute variant:** replace exercise 3 with a two-minute demo, stop exercise 4 after step 3, and skip the slides marked with the grey "60-min" badge (target-group order and filters, CLI tools).

## Before the session (presenter checklist)

1. Provide a SQL Server instance reachable from every participant's laptop, with a SQL login that may create databases (`dbcreator`), or one login per participant. The instance needs `TrustServerCertificate=True` in the connection string unless it has a trusted certificate.
2. Send [exercises/00-setup/README.md](exercises/00-setup/README.md) together with the server name and login at least two days ahead. Setup takes about ten minutes.
3. Create a database `BookStore_Demo` for your own demos (baseline in part 3) and run the setup check yourself.
4. Open the deck in presenter view and the Config Wizard at <https://config.raymigrator.com> in a second tab.
5. Keep the `exercises/solutions/` folder at hand for participants who fall behind.

## Rendering the deck

Marp is run through `npx`; Node.js 18 or later is required. PDF, PPTX and image exports need a local Chrome or Edge (Marp finds it automatically; set `CHROME_PATH` otherwise).

```bash
cd Workshop/slides

# HTML for presenting (press P for the presenter view with speaker notes, F for full screen)
npx @marp-team/marp-cli@latest raymigrator-workshop.md --html --theme-set themes -o raymigrator-workshop.html

# PDF handout
npx @marp-team/marp-cli@latest raymigrator-workshop.md --html --theme-set themes --allow-local-files --pdf -o raymigrator-workshop.pdf

# PowerPoint export, if a customer asks for one (speaker notes are exported as slide notes)
npx @marp-team/marp-cli@latest raymigrator-workshop.md --html --theme-set themes --allow-local-files --pptx -o raymigrator-workshop.pptx

# Live preview while editing
npx @marp-team/marp-cli@latest -s --html --theme-set themes .
```

`--html` is required because the two-column layouts use `<div>` elements. Rendered files are ignored by git.

In a script without a terminal (CI, a background job), append `< /dev/null` to each command; otherwise Marp waits for Markdown on standard input.

Diagrams are pre-rendered: edit the `.mmd` source in `slides/assets/` and re-run

```bash
npx @mermaid-js/mermaid-cli@latest -i assets/context.mmd -o assets/context.svg -c assets/mermaid-config.json -b transparent
```

(add `-p assets/puppeteer.json` to use a local Chrome instead of a downloaded Chromium).

## Verifying the exercises

Every command and every expected output in the exercise READMEs was captured from a real run of RayMigrator 0.15.0 against SQL Server 2022 in Docker (`Testing/Docker`, container `rm_db_sqlserver`). To re-verify after a RayMigrator release: create an empty database, point an `appsettings.Workshop.json` at it, and follow the four READMEs in order. The behaviours the exercises depend on are listed at the end of each README under "What you have seen".

## Licence

The workshop material is part of the RayMigrator repository and is licensed under the same terms, see [LICENSE.md](../LICENSE.md).
