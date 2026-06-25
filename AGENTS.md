# AGENTS.md — Repository Instructions

Repository-specific instructions for Codex.

Primary local environment: **Windows + PowerShell**.

Use repository conventions when they are clear. Keep changes focused, verifiable, and aligned with improving the current application rather than preserving obsolete behavior.

---

## 1. Priorities

1. Preserve user work.
2. Improve and simplify the current application.
3. Keep the repository clean and navigable.
4. Follow existing repository conventions.
5. Verify with available repository checks.
6. Keep `PROJECT_STATUS.json` as a todo-only file when present or requested.
7. Report changes, checks, and remaining uncertainty.

Do not change unrelated files. Do not introduce dependencies, config/deployment changes, broad refactors, migrations, or destructive operations unless they are needed to improve, clean up, or complete the requested work.

Do not claim a check passed unless it was actually run and passed.

---

## 2. Fresh project policy

Treat OnlyWinget as a new/greenfield application unless the user explicitly says it must integrate with legacy systems, existing production data, or old public APIs.

Prefer current conventions, clean architecture, minimal structure, and only the files needed for the current product direction.

You may make destructive internal changes when they improve correctness, maintainability, UX, or cleanup. This includes removing obsolete code paths, replacing flawed logic, changing internal data flow, rewriting local implementations, or deleting stale files. Preserve user work and avoid destructive Git operations unless explicitly requested.

Remove legacy code regularly. Do not add or keep compatibility facades, migration scaffolding, deprecated patterns, transitional folders, backward-compatibility shims, historical cleanup work, or assumptions about previous production users/data/APIs unless the user explicitly asks for them.

Maintain exactly one current implementation/version of each feature, data model, workflow, script, and document until the user explicitly orders otherwise. Do not keep parallel old/new implementations, duplicate scripts, alternate docs, compatibility modes, or versioned variants as a hedge. When replacing something, remove the old path in the same change when practical.

---

## 3. Windows-first rules

Use PowerShell-compatible commands unless repository evidence requires another shell, container, CI image, or deployment target.

Assume local development is Windows. Avoid local assumptions about Bash, WSL, GNU-only flags, `/tmp`, `/home`, `chmod`, `sed -i`, `rm -rf`, or Unix-only path separators.

Use repository-relative paths, quote paths that may contain spaces, avoid hardcoded absolute paths, and prefer cross-platform path APIs in code.

---

## 4. Workflow

For implementation tasks:

1. Inspect only files needed for the requested change, including applicable local instructions and nearby patterns.
2. Check the working tree before editing:

   ```powershell
   git status --short
   ```

3. Do not overwrite unrelated uncommitted user changes.
4. Implement the smallest production-quality change.
5. Add or update tests when behavior changes and a test framework exists.
6. Update docs only when setup, commands, behavior, public API, deployment, scripts, or structure change.
7. Run native formatting and linting regularly when practical.
8. Run the most relevant available checks.
9. Perform a cleanliness check before the final response.

For read-only tasks, do not modify files. Report findings, affected areas, recommended fixes, and review limits.

---

## 5. Cleanliness check

Before the final response for implementation tasks, verify that:

- unrelated files were not changed;
- new files are in the correct responsibility folder;
- generated, temporary, debug, build, report, or log files were not left in source folders;
- stale references were not left after renames or splits;
- duplicate scripts, configs, assets, or docs were not introduced;
- `.gitignore` covers local/generated outputs when appropriate.

Minimum commands when available:

```powershell
git status --short
git ls-files
```

Then run relevant project-native checks: format, lint, typecheck, tests, build, or security checks. State why any relevant check was not run.

---

## 6. `PROJECT_STATUS.json`

`PROJECT_STATUS.json` is optional unless already present or explicitly requested.

When it exists, it must contain **only current incomplete todo tasks**.

Allowed schema:

```json
{
  "todos": [
    "Short actionable task"
  ]
}
```

Rules:

- Remove completed, obsolete, duplicated, invalidated, or historical items.
- Do not store completed work, prompt/chat history, secrets, credentials, personal data, check results, decisions, assumptions, risks, blockers, timestamps, changelog entries, or status notes.
- Prefer fewer accurate todos over many stale todos.
- Do not update it for read-only tasks unless requested.

---

## 7. Structure and files

Use the existing structure when coherent. For new or unclear areas, start minimal and add folders only when real responsibilities exist.

Keep each source file focused on one responsibility. Split files only when responsibilities are mixed or maintenance is clearly worse without a split. Do not perform large unrelated splits.

Prefer explicit names. Avoid vague folders like `misc`, `stuff`, `old`, `new`, `final`, `temp2`, `backup`; avoid vague files like `utils.*`, `helpers.*`, `common.*`, `manager.*`, or unqualified `service.*` unless already established by the repository.

`index.*` files should contain exports, framework-required entrypoints, or very small composition code; not substantial implementation logic.

---

## 8. Scripts

Use repository-native scripts first.

When adding Windows-first local scripts, prefer PowerShell wrappers under `scripts/`.

Scripts must run from the repository root, validate required tools, fail with non-zero exit codes on errors, and avoid duplicating an existing script. Keep public scripts thin; put shared script logic in `scripts/support/` when needed.

Update `scripts/README.md` when public scripts are added, removed, or renamed.

Maintain one authoritative script per task. If a new script replaces an old command or wrapper, remove or update the old one in the same change when practical.

---

## 9. Verification

When behavior changes, add or update tests using the existing framework.

Run formatting and linting with repository-native tools regularly:

- Prefer existing scripts such as `scripts/format.ps1`, `scripts/lint.ps1`, `scripts/typecheck.ps1`, `scripts/test.ps1`, `scripts/build.ps1`, or `scripts/check.ps1` when present.
- If native format/lint tools are missing and code quality work requires them, add and configure appropriate project-native tooling and scripts instead of relying on ad hoc commands.
- Respect the existing package manager, lockfiles, and .NET tooling. Do not install tools only for convenience when an existing native tool already covers the need.

If no test framework exists, do not install one automatically unless required. Provide a practical manual verification path instead.

Run relevant available checks and report exact commands and results. Do not stop at the first failed check if useful static review can continue.

---

## 10. Security and dependencies

Never create, print, commit, or expose secrets.

Use `.env.example` for required environment variables. Do not put credentials, private keys, tokens, passwords, or personal data into docs, logs, examples, or status files.

Validate external input, escape output where required, use parameterized database queries, and avoid logging sensitive data.

Before adding a dependency, check whether the repository already has a suitable dependency. Prefer the standard library or existing utilities. Respect the existing package manager and lockfile. Do not add dependencies only for convenience.

---

## 11. Git hygiene

Inspect the working tree before editing. Do not overwrite user changes.

Avoid destructive Git operations: `git reset --hard`, `git clean -fd`, force pushes, branch deletion, and history rewriting.

Only stage or commit when explicitly requested.

---

## 12. Final response

For implementation tasks, include:

- what changed;
- files changed;
- checks run and results;
- cleanliness result;
- `PROJECT_STATUS.json` todo update, if applicable;
- remaining risks, blockers, or next steps.

For review-only tasks, include findings by severity, affected files/areas, suggested fixes, assumptions, and review limits.

Be factual and concise. Do not claim production readiness unless relevant checks passed or limitations are clearly stated.
