# AGENTS.md — OnlyWinget

## Product contract

- Work from the repository root on Windows with PowerShell.
- Target WinUI 3, .NET 10 LTS, stable Windows App SDK, and Windows 10 build 17763 or newer.
- Ship x64 only: WiX Burn/MSI installer plus a self-contained portable ZIP. Keep the installer’s Windows App Runtime x64 chain and the portable package’s `WindowsAppSDKSelfContained` publish.
- Treat the product as greenfield. Remove obsolete x86, pre-WinUI, compatibility, migration, dead-code, and historical scaffolding instead of extending it.
- Preserve English and Italian visible strings.

## Architecture

Dependencies remain one-way:

```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```

- Keep OS, API, PowerShell, winget, and Windows Update availability in `ISystemCapabilityService`.
- Guard external processes and COM calls, return structured failures, and keep actionable errors visible.
- Application state changes are instance-scoped through `OnlyWingetApplication.StateChanged`; do not add global static refresh events or manual page refresh broadcasts.
- Keep page item sources stable and update their collections. Prefer typed `x:Bind` for immutable presentation rows.
- Keep Windows Update scans explicit. Do not scan on page load or require elevation for read-only discovery.
- Preserve the workspace schema and preset exchange format unless the request explicitly changes them.
- Keep batch selection in reusable state logic; select-all must handle checked, unchecked, and mixed states.

## Packaging and setup

- Setup sources: `src/OnlyWinget.Setup`.
- Packaging entrypoint: `scripts/package.ps1`.
- Do not add a WiX project to the solution unless it builds in the current SDK workflow.
- Do not reintroduce x86 or `AnyCPU` artifacts.

## Backlog

`PROJECT_STATUS.json` is the complete, prioritized, residual-only backlog:

```json
{
  "todos": [
    "Short actionable task"
  ]
}
```

Remove completed, obsolete, duplicate, historical, and non-actionable entries. Keep only verified residual work in execution order. External or manual blockers must name the required environment.

## Workflow

1. Read applicable instructions and inspect only relevant files.
2. Run `git status --short`; preserve all existing user changes.
3. Make the smallest coherent change and add tests for behavior changes.
4. Use repository scripts, not ad hoc equivalents.
5. For WinUI changes, build, launch, verify a responsive top-level window, and leave the final verified app running.
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

Live discovery tests, when winget and Windows Update are available:

```powershell
.\scripts\run.ps1 -Task Test -Configuration Release -RunWingetSmoke -NonInteractive
```

UI automation against a running app:

```powershell
.\scripts\ui-test.ps1 -AppPid <PID> -NonInteractive
```

Installer lifecycle validation requires a clean elevated x64 Windows host:

```powershell
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

## Guardrails

- No unrelated refactors, dependencies, schema migrations, compatibility shims, secrets, or destructive Git commands.
- Use repository-relative paths in code and PowerShell-compatible commands in documentation.
- Validate external input and keep persistence writes transactional.
- Do not stage, commit, push, or rewrite history unless explicitly requested.

## Handoff

Report changes, files, checks and results, worktree cleanliness, `PROJECT_STATUS.json` changes, and remaining manual or external blockers.
