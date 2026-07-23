# AGENTS.md — OnlyWinget

## Product and platform

- Work from the repository root with PowerShell.
- Target WinUI 3, .NET 10 LTS, the stable Windows App SDK, and Windows 10 build 17763+.
- Ship x64 only: an NSIS setup installer and a self-contained portable ZIP.
- Keep `WindowsAppSDKSelfContained` publish for both installer and portable outputs.
- Preserve visible English and Italian strings.

## Greenfield policy

- Treat the product, UI, navigation, and presentation architecture as greenfield. Existing UI structure and public presentation types are not compatibility contracts.
- Keep the application version locked to version 1.0 (e.g., 1.0.x) until explicitly instructed otherwise by the user. Do not increment the major or minor version. No migration scaffolding or database migrations are needed at this stage.
- Prefer the clean target architecture over incremental compatibility. Destructive refactors, file and folder reorganization, replacement of presentation models, and deletion of superseded code are allowed when they produce a smaller coherent design.
- Do not add adapters, shims, deprecated aliases, parallel old/new implementations, feature flags, or migration scaffolding for the current UI unless the user explicitly requires a staged rollout.
- Remove obsolete x86, pre-WinUI, compatibility, migration, dead-code, duplicated UI, and historical scaffolding rather than extending or preserving it.
- Preserve only explicit product contracts: the dependency direction, workspace schema, preset exchange format, platform and packaging targets, behavioral guardrails below, and visible English and Italian strings.
- A broad redesign request authorizes coherent cross-cutting changes inside that redesign; it does not authorize unrelated product features or changes to the explicit contracts above.

## Architecture and behavior

Dependencies are one-way:

```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```

- Centralize OS, API, PowerShell, winget, and Windows Update availability in `ISystemCapabilityService`.
- Guard external processes and COM calls. Return structured failures and show actionable errors.
- Publish state changes through the instance-scoped `OnlyWingetApplication.StateChanged`; do not add static refresh events or manual page broadcasts.
- Serialize asynchronous operations in `OnlyWingetApplication`; disabled UI commands are not an application-layer concurrency guard.
- Parallelize read-only winget operations (searching, updates loading, and metadata resolution) via `Task.WhenAll` with concurrency throttled where appropriate (e.g. `SemaphoreSlim` limit of 4 for process execution).
- Ensure thread safety on shared memory updates (like `packageMetadata` additions) via `lock` sync primitives.
- Protect JSON-based store operations (`JsonWorkspaceStore`, `JsonSourcePreferenceStore`) using instance-scoped write/read serialization gates (`SemaphoreSlim(1,1)`).
- Keep page item sources stable and update their collections. Prefer typed `x:Bind` for immutable presentation rows.
- Ensure ViewModels bound via `x:Bind` are declared `public` (even if their constructors are `internal`) to permit compilation of bindings.
- Generate the grid-row cell layout programmatically in C# (e.g., using `OnlyWingetTableRow`) rather than compiling inline XAML strings at runtime via `XamlReader.Load` to ensure compile-time type safety.
- Run Windows Update scans only on explicit user action. Read-only discovery must not require elevation.
- Pass a real `CancellationToken` to every cancellable operation. Never enable cancellation for work started with `CancellationToken.None`.
- Preserve the workspace schema and preset exchange format unless explicitly requested otherwise.
- Keep batch selection in reusable state logic; select-all must support checked, unchecked, and mixed states.

## Packaging and restore

- Setup sources: `src/OnlyWinget.Setup`; packaging entrypoint: `scripts/package.ps1`.
- NSIS setup script: `src/OnlyWinget.Setup/OnlyWinget.nsi`.
- Do not introduce x86 or `AnyCPU` artifacts.
- Keep solution restore RID-neutral; the WinUI project and packaging scripts select `win-x64`.
- Keep the root NuGet cache ignore anchored as `/packages/`; `packages/` would also hide `src/OnlyWinget.Domain/Packages` on Windows.

## Backlog

`PROJECT_STATUS.json` is the complete, prioritized, residual-only backlog:

```json
{
  "todos": []
}
```

Keep only verified, actionable residual work in execution order. Remove completed, obsolete, duplicate, and historical entries. Manual or external blockers must name the required environment.

## Workflow

1. Update developer skills by running:
   ```powershell
   .\scripts\sync-win-dev-skills.ps1
   .\scripts\install-skills.ps1
   ```
   Always run these scripts when starting development or pulling updates to ensure local skills in `.agents/skills` are synchronized with the official skills in `skills/` (and Microsoft's official `win-dev-skills` repository). Then, inspect applicable developer skill instructions using `view_file`, check relevant source files, and run `git status --short` to preserve existing local changes.
2. Load and inspect relevant skills before any coding or architectural change:
   - For Clean Architecture layers, concurrency rules, and domain boundaries: [onlywinget](file:///.agents/skills/onlywinget/SKILL.md)
   - For WinUI 3 platform controls, layout, windowing, and App SDK: [winui](file:///.agents/skills/winui/SKILL.md)
   - For MVVM, `x:Bind` compile-time typed bindings, `DispatcherQueue` thread safety, and quality checks: [winui-code-review](file:///.agents/skills/winui-code-review/SKILL.md)
   - For Fluent Design System 2, light/dark theme resources, brushes, and designs: [winui-design](file:///.agents/skills/winui-design/SKILL.md)
   - For WinUI build workflow, debugging, and error diagnosis: [winui-dev-workflow](file:///.agents/skills/winui-dev-workflow/SKILL.md)
   - For WinUI packaging, self-contained deployment, and code signing: [winui-packaging](file:///.agents/skills/winui-packaging/SKILL.md)
   - For automated UI testing with winapp CLI: [winui-ui-testing](file:///.agents/skills/winui-ui-testing/SKILL.md)
   - For WPF/UWP to WinUI 3 migration patterns: [winui-wpf-migration](file:///.agents/skills/winui-wpf-migration/SKILL.md)
   - For WinUI 3 setup and SDK prerequisites: [winui-setup](file:///.agents/skills/winui-setup/SKILL.md)
   - For winget CLI commands, manifest schema, REST sources, and silent install switches: [winget-cli](file:///.agents/skills/winget-cli/SKILL.md)
   - For NSIS 3.x setup installer scripting, MUI2, x64 target directives, and uninstall registry keys: [nsis-installer](file:///.agents/skills/nsis-installer/SKILL.md)
3. Make the smallest coherent change. Add or update tests for behavior changes.
4. Use `scripts/run.ps1` tasks instead of ad hoc equivalents.
5. For WinUI changes, build, launch, confirm a responsive top-level window, and leave the verified app running. Terminate running application instances (e.g., `taskkill /f /im OnlyWinget.exe`) if clean tasks fail due to lock issues.
6. Before handoff, run `git status --short` and `git ls-files`.

## Commands

```powershell
.\scripts\run.ps1 -Task Setup -NonInteractive
.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Lint -NonInteractive
.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Build -Configuration Release -NonInteractive
.\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
```

After an intentional dependency, target-framework, or RID change, regenerate stale locked-restore files once:

```powershell
.\scripts\run.ps1 -Task Setup -ForceEvaluate -NonInteractive
```

Optional environment-dependent checks:

```powershell
# Requires winget and Windows Update
.\scripts\run.ps1 -Task Test -Configuration Release -RunWingetSmoke -NonInteractive

# Requires a running app and the winapp CLI
.\scripts\ui-test.ps1 -AppPid <PID> -NonInteractive

# Requires a clean, elevated x64 Windows host
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

For mouse-wheel UI tests, place the pointer over the target and, when it is scrollable, assert that its vertical scroll percentage changes. A wheel event alone is insufficient.

## Repository guardrails

- No refactors unrelated to the requested outcome, new dependencies without need, schema migrations, compatibility shims, or secrets.
- Use repository-relative paths in code and PowerShell-compatible commands in documentation.
- Validate external input and keep persistence writes transactional.
- Do not stage, commit, push, or rewrite history unless explicitly requested.

## Handoff

Report changed files, checks and results, worktree state, any `PROJECT_STATUS.json` update, and remaining manual or external blockers.
