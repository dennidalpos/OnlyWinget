# AGENTS.md — Project Instructions & Global Rules

## 1. Scope, Priority & Greenfield Mindset

* **Priority:** User request > Project `AGENTS.md` > Global defaults.
* **Zero Speculation / Directives First:** Follow user instructions strictly. Build ONLY what is specified. NEVER invent unasked features, speculative abstractions, extra architectural layers, temporary code, or unrequested dependencies.
* **Architecture:** Keep entrypoints thin; place logic in domain and application modules. Preserve existing architecture unless refactoring is explicitly requested.
* **Clean Code & Legacy Maintenance:** Develop clean, scalable code following modern best practices. Never leave dead, unused, or legacy code.
* **Uncertainty & Ambiguity:** If in doubt, stop immediately and ask the user before proceeding.

## 2. Environment (Windows & PowerShell)

* **Dev Environment:** **Windows / PowerShell / UTF-8**.
* **Syntax & Commands:** Avoid Unix-only syntax/commands (`/tmp`, `chmod`, `rm -rf`, `sed -i`).
* **Paths & APIs:** Use cross-platform APIs (`pathlib`), quoted paths, and repository-relative paths.
* **Git Status Check:** Check working tree (`git status --short`) before starting. Preserve pre-existing, unrelated work.

## 3. Strict Serial Execution & Testing

* **Sequential Workflows Only:** Execute ALL tests, builds, linters, and compilations strictly one by one in serial. Parallel/concurrent jobs are explicitly forbidden to prevent test flakiness, race conditions, and context/token waste.
* **Agent Fast Mode:** Automated tests run by AI must run in fast/summarized mode (concise output: `PASS/FAIL` + minimal stack trace on failure). PowerShell test scripts must support both `-Fast` mode (agent default) and `-Full` mode (manual debugging).
* **Honesty:** Update/add tests for code changes. Run sequential checks before reporting completion. Never claim checks or tests were run if they were skipped.

## 4. Dependencies, Security & Knowledge Sources

* **Dependencies & Universal Libraries:** Prefer established, complete, and universal standard libraries over fragile homegrown logic or hardcoded dictionaries. Keep dependencies updated, maintain lockfiles, and document all packages in `requirements.txt` / `package.json`.
* **Security:** Never expose, print, or commit secrets/tokens. Use `.env.example` and environment variables.
* **Forbidden Git Commands:** NEVER run `git reset --hard`, `git clean`, force-push, or history rewriting.
* **Information Sources:** Use official docs first. If unresolved, search community sources progressively down the hierarchy.

## 5. Repository Structure, Skills & Documentation

* **Scripts:** Place scripts in standard locations (default: `scripts/`), returning non-zero exit codes (`exit 1`) on failure and setting Fail-Fast behavior.
* **Active Tasks & Tracker Hygiene:** `PROJECT_STATUS.json` MUST contain exactly one top-level key, `todos` (a string array of PENDING work only), and nothing else — no `audit`/`verification`/`completed_*`/`blocked_*`/history/changelog/notes sections, and no per-task metadata objects (`id`, `priority`, `status`, `fix`, `description`, etc.). Schema is strictly `{"todos": ["Task"]}`. Add active tasks only. The INSTANT a task is completed, verified, or becomes obsolete, DELETE its entry immediately — never set a `done`/`noted` status and leave it in place, and never keep a running log of past findings "for reference". Audit findings, verification results, and rationale belong in the conversation and in git commit messages, never persisted in this file. Treat pruning as part of Definition of Done: before reporting any task as complete, remove its entry from `PROJECT_STATUS.json` and confirm the file still matches the strict schema above.
* **Project Skills:** Generate, maintain, and update project skills/capabilities as the repository grows. Store workspace skills in the root `/skills` directory (`skills/<skill_name>/SKILL.md`).
* **Documentation Maintenance:** Maintain minimal, concise project documentation. Update documentation immediately with code changes to prevent drift.

## 6. Contextual Skill Loading Instructions

AI Agents working in this repository MUST inspect and read the relevant `SKILL.md` instructions from `/skills` whenever engaging in tasks matching their scope:

* **[skills/onlywinget/SKILL.md](file:///d:/GITHUB/OnlyWinget/skills/onlywinget/SKILL.md)**: Trigger for Clean Architecture domain/application/infrastructure/presentation modifications, application lifecycle, dependency injection, and PowerShell run script workflows.
* **[skills/winget-cli/SKILL.md](file:///d:/GITHUB/OnlyWinget/skills/winget-cli/SKILL.md)**: Trigger for Windows Package Manager (`winget`) CLI integration, manifest schemas, source preference handling, silent install switches, exit codes, and process execution.
* **[skills/winui/SKILL.md](file:///d:/GITHUB/OnlyWinget/skills/winui/SKILL.md)**: Trigger for WinUI 3, Windows App SDK, Fluent Design UI layout, MVVM data binding, XAML controls, accessibility, and Native AOT / Trimming compilation rules.
* **[skills/nsis-installer/SKILL.md](file:///d:/GITHUB/OnlyWinget/skills/nsis-installer/SKILL.md)**: Trigger for NSIS 3.x setup scripting, Modern UI 2 (MUI2), x64 architecture targets, Windows registry uninstaller registration, and self-contained packaging.

## 7. Final Agent Response Format

Minimize response size to conserve tokens. Structure final responses strictly into:

1. **What changed:** Functional updates and structural improvements.
2. **Files modified/deleted:** List of created, edited, or deleted files.
3. **Checks run & results:** Real results for sequential tests/builds (or explicit reason if skipped).
4. **Cleanliness status:** Confirmation that no temp/secret files remain.
5. **Remaining limitations / risks:** Concrete risks only.
6. **Next steps:** Immediate necessary follow-ups only.

## 8. Layered Architecture & Clean Code Directives

* **Layered Architecture Model (Presentation → Application → Domain → Infrastructure):**
  - **Presentation / UI / API Layer:** Input validation, UI/HTTP routing only. No business logic or direct DB queries.
  - **Application / Service Layer:** Use cases & business logic. Orchestrates data between Domain Layer and external dependencies.
  - **Domain Layer:** Core entities & pure domain rules. Zero dependencies on external frameworks or ORMs.
  - **Infrastructure / Data Layer:** Persistence (DB, ORM), external API calls, third-party services. Implements interfaces defined by upper layers (Dependency Inversion).
  - *Strict Rule:* Never skip layers (e.g., UI controllers must never call repositories or DB directly).
* **Clean Code Standards:**
  - **SRP:** Single Responsibility Principle for every file, class, and function. Split a file when it mixes unrelated responsibilities or domains, not to hit a line count — a cohesive, single-purpose file stays whole regardless of length. Decompose along real seams (a distinct concern, a distinct domain concept, a distinct layer), never by mechanically slicing a large-but-coherent file into arbitrary chunks.
  - **Function Length:** Prefer short, focused functions; extract private sub-functions when a function mixes multiple steps or responsibilities — not to satisfy a line-count target.
  - **Strong Typing:** Explicit types mandatory (TypeScript strict, Python Type Hints, etc.). Avoid generic `any` or `Object`.
  - **Boundary Error Handling:** Handle errors at boundaries. Return or throw explicit typed errors; no empty `try/catch` or silently swallowed errors.
* **Agent Execution Workflow:**
  1. **Planning:** Briefly describe scope, targeted files, and interfaces before writing code.
  2. **Architectural Impact:** Ensure each new file is placed in its correct layer directory.
  3. **Test-First / Test-Coherent:** Create or update unit tests for all business logic changes.

## 9. Documentation Protocol

- **Source of Truth:** Tutta la documentazione risiede solo in `/docs/`.
- **Sync & Prune Obbligatorio:** A ogni modifica di codice (architettura, moduli, API, env/setup), aggiorna i file corrispondenti in `/docs/` ed elimina immediatamente sezioni o file diventati obsoleti.
- **Definizione di "Done":** Qualsiasi modifica priva del contestuale aggiornamento di `/docs` e considerata incompleta.