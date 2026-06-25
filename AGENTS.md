# AGENTS.md

Repository instructions for Codex in OnlyWinget.

## Defaults

- Environment: Windows, PowerShell, repository root commands.
- Project policy: treat OnlyWinget as greenfield unless the user says otherwise.
- Prefer small, clean, current implementations over compatibility layers.
- Preserve user work. Never run destructive Git commands unless explicitly requested.
- Keep `PROJECT_STATUS.json` as todo-only JSON:

```json
{
  "todos": [
    "Short actionable task"
  ]
}
```

Remove completed, obsolete, duplicate, historical, or non-actionable items.

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
.\scripts\package.ps1
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
