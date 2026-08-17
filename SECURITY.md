# Security Policy

## Supported versions

RayMigrator is pre-1.0. Security fixes are provided for the **latest
released 0.11.x version only**; older versions do not receive patches.
Each release lists its dependency updates and any security-relevant
changes in [CHANGELOG.md](CHANGELOG.md).

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

- **Preferred:** use GitHub's private vulnerability reporting on this
  repository ("Security" → "Report a vulnerability"), if available.
- **Alternatively:** e-mail `raymigrator@raycoon.com` with the subject
  prefix `[SECURITY]`.

Please include what you can of the following: affected component (CLI,
a specific DAL plugin, Config Wizard) and version, a description of the
issue and its impact, and steps or a proof of concept to reproduce it.

## What to expect

- We aim to acknowledge your report within **5 business days**.
- We will investigate, keep you informed of the outcome, and coordinate
  the disclosure timing with you before details are published.
- Fixes ship as a new release; the advisory credits the reporter unless
  you prefer otherwise.
- There is currently no bug bounty program.

## Scope

In scope: the RayMigrator engine and CLI, the DAL plugins in this
repository, the release artifacts, and the Config Wizard
(`config.raymigrator.com`).

Out of scope: vulnerabilities purely in third-party dependencies
(please report them upstream — but do tell us so we can update), and the
content of the marketing website.

## A note on deployments

RayMigrator executes SQL against databases you configure. Treat
connection strings as secrets (use the `{ENV:...}` placeholder mechanism
rather than plain-text credentials), restrict the database accounts you
give it to what your migrations need, and keep a verified backup before
every run — see the maturity notice in [NOTICE.md](NOTICE.md).
