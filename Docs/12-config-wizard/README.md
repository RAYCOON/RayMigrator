# Config-Wizard Documentation

Documentation for the Config-Wizard ecosystem, which spans two projects:
- `Raycoon.RayMigrator.ConfigWizard.Core` — shared domain library (models, services, validation) used by the Web wizard
- `Raycoon.RayMigrator.ConfigWizard.Web` — Blazor WASM standalone wizard (MudBlazor, multilingual DE/EN)

## Contents

| File | Description |
|------|-------------|
| [overview.md](./overview.md) | Introduction, Web wizard flow, ecosystem overview |
| [architecture.md](./architecture.md) | Models, enums, WizardState, WizardPhase |
| [services.md](./services.md) | Service class reference (ConfigurationSerializer, ConfigFileMerger, etc.) |
| [file-hierarchy.md](./file-hierarchy.md) | 4-level appsettings hierarchy, merge semantics, file family discovery |
| [validation.md](./validation.md) | Validation rules per section, field-level rules, ENV placeholders |

## Quick Reference

### Project

- Source (Web): `Raycoon.RayMigrator.ConfigWizard.Web/` — Blazor WASM standalone app (MudBlazor 9, multilingual DE/EN, hub-and-spoke wizard: Welcome → Hub → Detailed Config → Overview)
- Shared domain library: `Raycoon.RayMigrator.ConfigWizard.Core/` (zero NuGet/project dependencies, multi-target)
- Tests (Core): `Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core/`
- Tests (Web): `Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web/`
- Target frameworks: `net10.0`, `net9.0`, `net8.0` (Core); `net10.0` (Web)
