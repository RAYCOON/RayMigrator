# RayMigrator Architecture Documentation

This wiki contains the architecture documentation of RayMigrator, structured
according to the [arc42](https://arc42.org) template. RayMigrator is a cross
platform database migration framework for versioned, release based schema
migrations across SQL Server, PostgreSQL, MariaDB, MySQL and SQLite.

The pages here describe *why* the system is built the way it is and *how* its
parts fit together. The detailed implementation and configuration reference
lives in the repository under
[`Docs/`](https://github.com/RAYCOON/RayMigrator/tree/main/Docs) and is linked
from each chapter.

## Chapters

| # | Chapter | Answers |
|---|---------|---------|
| 1 | [Introduction and Goals](01-Introduction-and-Goals.md) | What is RayMigrator, for whom, and what must it achieve? |
| 2 | [Architecture Constraints](02-Architecture-Constraints.md) | Which technical, organizational and legal constraints shape it? |
| 3 | [Context and Scope](03-Context-and-Scope.md) | Where are the system boundaries, who and what interacts with it? |
| 4 | [Solution Strategy](04-Solution-Strategy.md) | Which fundamental decisions and approaches drive the design? |
| 5 | [Building Block View](05-Building-Block-View.md) | How is the system decomposed into projects, layers and components? |
| 6 | [Runtime View](06-Runtime-View.md) | How do the building blocks interact in the important scenarios? |
| 7 | [Deployment View](07-Deployment-View.md) | How is RayMigrator built, packaged, distributed and run? |
| 8 | [Crosscutting Concepts](08-Crosscutting-Concepts.md) | Which concepts apply across the whole system? |
| 9 | [Architecture Decisions](09-Architecture-Decisions.md) | Which important decisions were made, and why? |
| 10 | [Quality Requirements](10-Quality-Requirements.md) | Which quality goals matter, expressed as concrete scenarios? |
| 11 | [Risks and Technical Debt](11-Risks-and-Technical-Debt.md) | What are the known risks and debts? |
| 12 | [Glossary](12-Glossary.md) | Which terms are used, and what do they mean? |

## Maintaining these pages

The wiki is generated from
[`Docs/arc42/`](https://github.com/RAYCOON/RayMigrator/tree/main/Docs/arc42)
on the `main` branch. Edit the Markdown files there and open a pull request;
edits made directly in the wiki are overwritten by the next sync.
