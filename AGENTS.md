# AGENTS.md

Repository instructions for Codex in OnlyWinget.

## Defaults

- Environment: Windows, PowerShell, repository root commands.
- Project policy: treat OnlyWinget as greenfield unless the user says otherwise.
- Current product direction: maintain the WinUI 3 desktop app on .NET 10 LTS, using Windows App SDK stable and keeping WiX Burn as the installer channel.
- Treat pre-WinUI compatibility code, transitional migration scaffolding, and obsolete compatibility notes as removable unless a current task explicitly requires them.
- Prefer small, clean, current implementations over compatibility layers.
- Preserve user work. Never run destructive Git commands unless explicitly requested.
- Keep `PROJECT_STATUS.json` as the complete prioritized residual plan and todo-only JSON:

```json
{
  "todos": [
    "Short actionable task"
  ]
}
```

Keep the list in execution order. Remove completed, obsolete, duplicate, historical, or non-actionable items. Add newly identified blockers when local inspection finds them, especially dirty tracked files that must be resolved before release validation.

## Current target

- Target stack: WinUI 3, .NET 10 LTS, Windows App SDK stable, Windows 10 1809 build 17763 minimum.
- Architecture target: Clean Architecture with Domain, Application, Infrastructure, and WinUI Presentation layers with one-way dependencies.
- Installer target: keep WiX Burn/MSI packaging and chain the Windows App Runtime redistributable required by Windows App SDK.
- Packaging target: full bundle packaging requires `WindowsAppRuntimeInstall.exe` matching `Microsoft.WindowsAppSDK`, supplied by `-WindowsAppRuntimeInstallerPath` or `ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER` when it is not in the local NuGet cache.
- Data policy: use the current workspace schema; do not add migration layers unless a later user request changes this.
- Localization policy: preserve Italian and English visible UI strings in the new presentation layer.
- Selection policy: implement batch selection in reusable state logic; header select-all must reliably toggle all rows from checked, unchecked, and mixed states.
- Backlog source: `PROJECT_STATUS.json` is the prioritized residual implementation, validation, and release backlog.

## Workflow

1. Inspect only relevant files and instructions.
2. Check `git status --short` before edits.
3. Make focused changes consistent with existing conventions.
4. Add or update tests when behavior changes.
5. Run the most relevant repo-native checks.
6. Before final response, run `git status --short` and `git ls-files`.

## Commands

Use scripts before ad hoc commands:

```powershell
.\scripts\setup.ps1
.\scripts\format.ps1
.\scripts\lint.ps1
.\scripts\typecheck.ps1
.\scripts\test.ps1
.\scripts\build.ps1
.\scripts\package.ps1 -WindowsAppRuntimeInstallerPath C:\Path\To\WindowsAppRuntimeInstall.exe
.\scripts\check.ps1
```

Run live `winget` smoke tests only when explicitly needed:

```powershell
.\scripts\test.ps1 -RunWingetSmoke
.\scripts\check.ps1 -RunWingetSmoke
```

Run installer lifecycle validation only on a clean elevated Windows host:

```powershell
.\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore
```

## Guardrails

- No unrelated refactors, dependencies, migrations, config churn, or broad rewrites.
- No secrets in source, docs, examples, logs, or status files.
- Use `.env.example` only when environment configuration must be documented.
- Use repository-relative paths and cross-platform path APIs in code.
- Avoid Bash, WSL, GNU-only flags, `/tmp`, `/home`, `chmod`, `sed -i`, and `rm -rf` assumptions.
- Do not stage or commit unless asked.

## Final Response

For implementation work, report:

- what changed;
- files changed;
- checks run and results;
- cleanliness result;
- `PROJECT_STATUS.json` update, if any;
- remaining risks or manual follow-ups.
