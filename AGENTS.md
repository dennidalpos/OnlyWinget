# AGENTS.md

Repository instructions for Codex in OnlyWinget.

## Project Contract

- Environment: Windows, PowerShell, run commands from the repository root.
- Treat OnlyWinget as greenfield unless a user request says otherwise.
- Product target: WinUI 3 desktop app on .NET 10 LTS, Windows App SDK stable, Windows 10 1809 build 17763 minimum.
- Installer target: keep WiX Burn/MSI packaging and chain the Windows App Runtime redistributable required by `Microsoft.WindowsAppSDK`.
- Architecture target: Domain, Application, Infrastructure, and WinUI Presentation layers with one-way dependencies:

```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```

- Setup sources live in `src/OnlyWinget.Setup` and are packaged by `scripts/package.ps1`; do not add a WiX project to the solution unless it builds cleanly in the current SDK workflow.

## Current Design Rules

- Centralize OS/API/tool availability in the system capability service. Do not scatter direct checks for OS build, `winget`, PowerShell, or Windows Update COM across UI or feature code.
- Guard feature execution before invoking external processes or Windows APIs. Return structured failures and user-visible fallback messages instead of crashing.
- Keep Windows Update explicit: do not auto-scan on page load.
- Preserve the current workspace schema and preset exchange format. Do not add migrations, compatibility shims, or transitional code unless explicitly requested.
- Preserve Italian and English visible UI strings.
- Keep batch selection in reusable state logic. Header select-all must work from checked, unchecked, and mixed states.
- Prefer removing obsolete pre-WinUI compatibility code, dead abstractions, unused types, and historical notes over preserving them.

## Backlog

`PROJECT_STATUS.json` is the complete prioritized residual backlog and must stay todo-only JSON:

```json
{
  "todos": [
    "Short actionable task"
  ]
}
```

Keep todos in execution order. Remove completed, obsolete, duplicate, historical, or non-actionable items. Add only real residual blockers discovered by inspection.

## Workflow

1. Inspect relevant files and applicable instructions.
2. Check `git status --short` before edits.
3. Preserve user work; never overwrite unrelated dirty files.
4. Make focused changes consistent with the current architecture.
5. Add or update tests when behavior changes.
6. Use repository scripts before ad hoc commands.
7. Before the final response, run `git status --short` and `git ls-files`.

## Commands

Use these entrypoints:

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

Run live `winget` smoke tests only when explicitly needed and the host has `winget` installed/configured:

```powershell
.\scripts\run.ps1 -Task Test -Configuration Release -RunWingetSmoke -NonInteractive
.\scripts\run.ps1 -Task Check -Configuration Release -RunWingetSmoke -NonInteractive
```

Run installer lifecycle validation only on a clean elevated Windows host:

```powershell
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

## Guardrails

- No unrelated refactors, dependencies, migrations, config churn, or broad rewrites.
- No secrets in source, docs, examples, logs, or status files.
- Use `.env.example` only when environment configuration must be documented.
- Use repository-relative paths and cross-platform path APIs in code.
- Avoid Bash, WSL, GNU-only flags, `/tmp`, `/home`, `chmod`, `sed -i`, and `rm -rf` assumptions.
- Do not stage, commit, force-push, rewrite history, or run destructive Git commands unless explicitly asked.

## Final Response

For implementation work, report:

- what changed;
- files changed;
- checks run and results;
- cleanliness result;
- `PROJECT_STATUS.json` update, if any;
- remaining risks or manual follow-ups.
