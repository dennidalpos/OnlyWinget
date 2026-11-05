# OnlyWinget documentation

[Italian reference](README.it.md)

OnlyWinget manages reusable application presets, searches WinGet sources and reviews application/Windows updates through an English/Italian WinUI 3 interface.

| Reader | Guide | Contents |
|---|---|---|
| New user | [README](../README.md) | Requirements, supported distributions, first run, local data and limits |
| End user | [User guide](user-guide.md) | Presets, search, updates, sources, activity and preferences |
| Developer | [Architecture](architecture.md) | Dependencies, composition, storage, native processes and UI |
| Maintainer | [Operations](operations.md) | Commands, verification, data isolation, cancellation and recovery |
| Release maintainer | [Release](release.md) | Versioning, gates, NSIS setup, portable ZIP and qualification limits |
| Script user | [Script reference](../scripts/README.md) | Task runner, parameters, prerequisites and regressions |

The primary user guide and index are maintained in English; their former Italian pages remain linked counterparts. Diagrams describe implemented flows rather than proof of a successful installation or release gate. Dated audit/tracker-review documents retain the original scenario, revision and language.

Source of truth: [projects](../src/), [task runner](../scripts/run.ps1), [SDK configuration](../global.json) and [workflow sources](../.github/workflows/). Shared agent instructions are in [AGENTS.md](../AGENTS.md); private development knowledge belongs in the SecondBrain dossier.

Use [Security](../SECURITY.md) for vulnerability reports and [Contributing](../CONTRIBUTING.md) for changes.
