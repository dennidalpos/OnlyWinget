# AGENTS.md — OnlyWinget

## Product and platform

- Work from the repository root with PowerShell.
- Target WinUI 3, .NET 10 LTS, the stable Windows App SDK, and Windows 10 build 17763+.
- Ship x64 only: a WiX Burn/MSI installer and a self-contained portable ZIP.
- Keep the installer Windows App Runtime x64 chain and portable `WindowsAppSDKSelfContained` publish.
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
- Keep page item sources stable and update their collections. Prefer typed `x:Bind` for immutable presentation rows.
- Run Windows Update scans only on explicit user action. Read-only discovery must not require elevation.
- Pass a real `CancellationToken` to every cancellable operation. Never enable cancellation for work started with `CancellationToken.None`.
- Preserve the workspace schema and preset exchange format unless explicitly requested otherwise.
- Keep batch selection in reusable state logic; select-all must support checked, unchecked, and mixed states.

## Packaging and restore

- Setup sources: `src/OnlyWinget.Setup`; packaging entrypoint: `scripts/package.ps1`.
- Do not add the WiX project to the solution unless it builds in the current SDK workflow.
- Do not introduce x86 or `AnyCPU` artifacts.
- Keep solution restore RID-neutral; the WinUI project and packaging scripts select `win-x64`.
- Keep the root NuGet cache ignore anchored as `/packages/`; `packages/` would also hide `src/OnlyWinget.Domain/Packages` on Windows.

## Backlog

`PROJECT_STATUS.json` is the complete, prioritized, residual-only backlog:

```json
{
  "todos": ["Short actionable task"]
}
```

Keep only verified, actionable residual work in execution order. Remove completed, obsolete, duplicate, and historical entries. Manual or external blockers must name the required environment.

## Workflow

1. Inspect applicable instructions, relevant files, and `git status --short`; preserve existing changes.
2. Make the smallest coherent change. Add or update tests for behavior changes.
3. Use `scripts/run.ps1` tasks instead of ad hoc equivalents.
4. For WinUI changes, build, launch, confirm a responsive top-level window, and leave the verified app running.
5. Before handoff, run `git status --short` and `git ls-files`.

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
