---
name: onlywinget
description: "Develop, maintain or audit the OnlyWinget .NET 10 WinUI 3 application, its WinGet CLI and Windows Update workflows, and NSIS/portable packaging."
---

# OnlyWinget maintenance

Start with [AGENTS.md](../../../AGENTS.md), [PROJECT_STATUS.json](../../../PROJECT_STATUS.json) and the current diff. They define the checkout, pending work and verification evidence. For an open validation item, also read the newest tracker-review handoff named by the tracker before treating an older result as current.

## Read only the relevant reference

- [Architecture invariants](references/architecture.md): persistence, workflow ownership, diagnostics and native update behavior.
- [Commands](references/commands.md): build/test/package tasks and validation boundaries.
- [Maintained architecture](../../../docs/architecture.md), [operations](../../../docs/operations.md) and [release](../../../docs/release.md) are canonical product documentation.

## Repository decisions

- Presentation depends inward on Application/Domain; Infrastructure implements Application ports. Infrastructure references in UI belong only in AppComposition.
- Use ProcessExternalProcessRunner and ArgumentList for external processes. WinGet integration uses the CLI; asynchronous COM jobs are for Windows Update.
- TextResources.cs and Localize provide EN/IT localization. Keep existing field-based CommunityToolkit generators and typed UiCommand patterns; do not migrate them just to satisfy a generic sample.
- OnlyWingetTable uses ListView/ItemsStackPanel virtualization. Preserve shared column sizing and selection behavior.
- Deployment is unpackaged, self-contained win-x64, with asInvoker and NSIS per-user/per-machine scopes. URL activation is retired. Do not introduce MSIX, ARM64 or a protocol handler through generic WinUI guidance.
- Check relevant native contracts against current primary sources. Do not equate compilation/offline regressions with interactive UI, live package/update operations or hosted release qualification.
- For UI validation that can change persisted state, use an isolated fixture or profile by default. Direct validation against personal data needs explicit authorization, narrowly scoped backups/restoration evidence, and no broad profile-directory swaps. UI automation may close only a dialog whose ownership chain and process identity are verified.
- Dialogs opened from a themed root must honor that root's actual theme. Keep log-viewer actions accessible with explicit localized level text; a successful build is not evidence for picker, modal-lifetime or High Contrast behavior.
- Update documentation after task completion, remove closed tracker entries and record remaining validation. Do not copy historical counts into current evidence.
