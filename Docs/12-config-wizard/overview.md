# Config-Wizard Overview

The **RayMigrator Config-Wizard** ecosystem eliminates the need to hand-edit JSON configuration files. It consists of two projects:

- **`Raycoon.RayMigrator.ConfigWizard.Core`** — shared domain library (models, services, validation). Has zero NuGet and zero project dependencies; targets `net10.0`, `net9.0`, and `net8.0`. Used by the Web wizard and independently unit-tested.
- **`Raycoon.RayMigrator.ConfigWizard.Web`** — Blazor WASM standalone wizard. Targets `net10.0`. Uses MudBlazor 9. Supports English and German (DE/EN). All validation runs client-side (no server required).

## Purpose

The wizard:

- Guides users through the complete configuration hierarchy step by step
- Validates all settings in real time
- Manages the 4-level `appsettings` file hierarchy (Base, Environment, Product, ProductEnvironment)
- Generates skeleton environment files and `example.env` files
- Generates a starter migration file directory structure (scaffold)
- Provides context-sensitive help for every field (multilingual)

## Web Wizard Flow

The Blazor Web wizard follows a hub-and-spoke flow with four top-level phases:

```
Phase 1: Welcome
  └─► Start from scratch → scaffold minimal WizardState
      Import existing files → parse uploaded appsettings*.json

Phase 2: Hub
  └─► Products and Environments matrix
      ├── Add/remove products and environments
      ├── View TODO/DONE status per product+environment combination
      └── Enter Detailed Configuration for any combination

Phase 3: Detailed Configuration
  └─► Step-by-step stepper (up to 6 steps, scoped to one product+environment):
      1. Repository
      2. DatabaseLogging
      3. CliTools (Expert Mode only)
      4. ProductDefaults
      5. ProductSettings
      6. Serilog
      ─► Complete → back to Hub

Phase 4: Overview
  └─► Full configuration overview:
      ├── Validation report
      ├── Product × Environment matrix with status
      ├── JSON preview per file (diff-based)
      ├── Auto-promote defaults and validate (on page load)
      └── Export as ZIP (all appsettings*.json files)
```

The `ProgressIndicator` component displays the three main phases (Hub → Configuration → Overview) and highlights the current one.

## Related Documentation

- [Architecture and Models](./architecture.md)
- [Services Reference](./services.md)
- [Configuration File Hierarchy](./file-hierarchy.md)
- [Validation Reference](./validation.md)
- [Unit Tests](../10-testing/unit-tests.md#configwizardcore-unit-tests)
- [Configuration Reference](../06-configuration-reference/appsettings-hierarchy.md)
