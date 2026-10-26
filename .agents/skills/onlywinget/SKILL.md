---
name: onlywinget
description: "Develop, maintain or audit the OnlyWinget .NET 10 WinUI 3 application, its WinGet CLI and Windows Update workflows, and NSIS/portable packaging."
---

# OnlyWinget maintenance

Start with [AGENTS.md](../../../AGENTS.md) and 02-areas/products/OnlyWinget/tracker.json (nel dossier SecondBrain) (SecondBrain: `02-areas/products/OnlyWinget/tracker.json`) for project constraints and active backlog items.

## References

- [Architecture invariants](references/architecture.md): layer boundaries, SQLite persistence, concurrency, diagnostics, WinGet CLI and Windows Update execution.
- [Commands](references/commands.md): task runner entrypoints, offline regressions and verification boundaries.
- Canonical documentation: [docs/README.md](../../../docs/README.md), [architecture.md](../../../docs/architecture.md), [operations.md](../../../docs/operations.md), [release.md](../../../docs/release.md) and [user-guide.md](../../../docs/user-guide.md).

## Repository decisions

- Presentation (`src/OnlyWinget`) depends inward on Application (`src/OnlyWinget.Application`) and Domain (`src/OnlyWinget.Domain`); Infrastructure (`src/OnlyWinget.Infrastructure`) implements Application ports. Infrastructure references in UI belong only in `src/OnlyWinget/AppComposition.cs`.
- External processes run through `ProcessExternalProcessRunner` with discrete `ProcessStartInfo.ArgumentList` parameters (never shell concatenation). WinGet uses the CLI runner stack; Windows Update uses asynchronous COM jobs (`ComWindowsUpdateService`) with an asynchronous PowerShell fallback (`PowerShellWindowsUpdateService`) when COM cannot be activated.
- `src/OnlyWinget/TextResources.cs` and `Services/LocalizeExtension.cs` (`Localize`) provide EN/IT localization. Keep existing field-based `CommunityToolkit.Mvvm` `[ObservableProperty]` generators and typed `UiCommand` patterns.
- `Controls/OnlyWingetTable` uses `ListView`/`ItemsStackPanel` virtualization. Preserve shared column sizing, stable observable collections and selection behavior.
- Deployment is unpackaged, self-contained `win-x64` (`WindowsPackageType=None`, `asInvoker`) distributed via NSIS multi-user setup (`src/OnlyWinget.Setup/OnlyWinget.nsi`) and portable ZIP. The legacy `onlywinget://` URL protocol is retired; startup and uninstall perform only ownership-verified legacy HKCU cleanup.
- For interactive UI validation that mutates persisted state, launch with process-scoped `ONLYWINGET_DATA_ROOT` set to a fully qualified isolated directory. UI automation (`scripts/ui-test.ps1`) may close only dialogs whose native owner chain and PID match the tested instance.
- Dialogs opened from a themed root must honor that root's actual theme. Keep log-viewer actions accessible with explicit localized level text and inline clear confirmation inside `Controls/LogViewerDialog`.
