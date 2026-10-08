# Tracker review — 2026-10-08

Reviewed [PROJECT_STATUS.json](../PROJECT_STATUS.json), the [latest remediation](audit-remediation-2026-10-08.md), canonical documentation and the source paths for outstanding work. Starting checkout: clean `main`, `HEAD`, local `origin/main` and the remote branch all resolve to `353c9b983fcc5b0561312f1729d4555316a06cbb`. Remote equality was checked with `git ls-remote`; no commit or push was performed during this continuation.

## Completed — documentation consistency

The README still advertised the retired DPAPI service. Operations documented accepted preset autosave and log retention as missing under already completed AUDIT-35/36, then described the implemented behavior in an unrelated section. Source review confirms `PresetsViewModel.SetActivePresetAsync` saves successful selections and `DiagnosticLogStore` defaults to 10 MiB files and 14 retained files.

Updated the README to describe retained legacy files rather than an active secret-storage service. Consolidated autosave under workspace saving and rolling/retention under diagnostic logs. Source-preference recovery has its own section. Removed the duplicate, contradictory guidance and corrected the README's storage terminology. No runtime code, stored data, dependency, command or schema changed.

Verification: baseline local README/AGENTS/docs links resolve and `git diff --check` passes. Post-edit checks validate local links, tracker JSON, unique pending entries and the absence of the stale DPAPI/autosave/retention claims. Compilation and application tests are not rerun for documentation-only changes; the previous session's 364-test result remains historical evidence, not a new execution.

## Tracker cleanup

Removed the session-handoff entry after verifying that its save commit is present on the remote default branch and that the starting checkout is clean. The ten completed local tasks listed by that handoff were already absent from pending work. This continuation closes documentation discrepancies only; no live-validation task is treated as completed from source review.

## Remaining prerequisites — 12 pending entries

| Priority / entry | Required evidence or input | Current result |
| --- | --- | --- |
| P1 AUDIT-03/04 | Controlled Windows machine with available updates for actual download/install, abort and exact revision/selection scenarios. | Not run; offline/native-search evidence does not qualify real installation. |
| P1 AUDIT-10 | Isolated profile for actual brokered import cancellation while an unrelated picker stays open. | Not run; native ownership fixtures remain mechanism evidence. |
| P1 AUDIT-12 | Hosted branch/tag mismatch and failed-gate publication-prevention qualification. | GitHub reports zero `release.yml` workflow runs; no workflow dispatched. |
| P2 AUDIT-19 | Isolated profile for source-aware editing, save failures, dismissal, guarded navigation and close/reentrancy. | Not run; normal application paths use personal LocalApplicationData. |
| P2 AUDIT-22 | Isolated profile for real clear/copy/export outcomes, close during export and Light/Dark/High Contrast. | Not run; localization and compilation do not qualify picker lifetime or rendering. |
| P2 AUDIT-27 | Real WinUI focus/toggle, row/preset selection, picker cancellation and resized layout. | Not run; the separate UIA snapshot fixture does not qualify the application. |
| P2 AUDIT-31 | Genuine earlier installer/payload, including removed/renamed owned files, plus controlled installation state. | GitHub release inventory is empty. Current installer still extracts the current ownership list without reconciling obsolete previous files. No legacy uninstaller executed. |
| P3 deployed landing | Actual deployed URL and accessible HTML/downloads for exact-version/hash verification. | GitHub Pages endpoint returns HTTP 404; deployment cannot be inferred from local landing files. |
| P3 long-idle blank window | User-machine observation and reproduction evidence over time. | No reproduction established during this session. |
| P3 AUDIT-37 | Ownership evidence for registrations from deleted copies or another user's profile. | No personal registry enumeration or deletion. |
| P3 skill-validation cleanup | Resolution of the automatic review rejection or manual cleanup of the exact session fixture. | Read-only inspection confirms 44 files and no reparse points. One guarded removal attempt in this continuation was rejected before execution with `blocked by policy`; the residue remains. |

The user confirmed that no isolated test profile/machine is available and requested repository-only work, then explicitly selected SQLite migration. That repository task is completed below; interactive/live validation remains pending. A genuine earlier installer and deployed landing URL are also still unavailable. Pending entries retain these prerequisites without duplicate tasks.

Primary sources checked during this review: [EF Core Create and Drop APIs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created), [NSIS scripting reference](https://nsis.sourceforge.io/Docs/Chapter4.html) and [GitHub Pages site API](https://docs.github.com/en/rest/pages/pages#get-a-github-pages-site). Microsoft documents that `EnsureCreatedAsync` leaves existing tables unchanged and is not a seamless migration path. The authorized native migration below replaces this initialization path.

## Skill-validation cleanup — blocked again

Read-only inventory confirmed the exact session-owned PyYAML fixture under `tmp/skill-validation-a74b195f6307432291944e7f020d607c`, with 44 files and no reparse points. The original `Remove-Item -LiteralPath ... -Recurse -Force` operation was attempted once, guarded by the exact absolute path, containment within this checkout's `tmp`, unchanged file count and entry ownership. Automatic execution review returned `exec_command failed: CreateProcess ... rejected: blocked by policy` before running the command. No alternative deletion mechanism was attempted; the task remains open. This is an execution-policy limitation, not a failed application test.

## Completed — SQLite schema migration

The user selected migration rather than retaining the unused columns. Removed Description/CreatedAt/UpdatedAt/PackageName from the EF entities and both workspace write paths. Initialization now uses native SQLite `user_version` instead of `EnsureCreated`: fresh databases get the reduced EF-generated schema at version 1; the recognized version-0 schema is backed up and upgraded. Connection strings use the provider builder so path punctuation cannot change connection options. No dependency, lockfile, application storage interface or personal database changed.

An immediate native transaction reserves the writer before inspecting the schema. Column types, nullability, primary keys, preset-name uniqueness and the cascading item foreign key must match the supported schema. Future/negative versions, unexpected columns, missing constraints, unrelated-only tables/views and invalid foreign keys fail before alteration. A private read connection backs up committed data, including WAL content, while the writer reservation prevents concurrent writes. The unique `.bak.partial` file is verified, closed and flushed before becoming the retained `.bak` snapshot. Backup failure blocks migration. DDL, integrity/foreign-key checks and the version marker share one transaction; no automatic fallback or custom retry loop is introduced.

Verification:

| Check | Actual result |
| --- | --- |
| SQLite baseline, before source edits | 6 passed, 0 failed. |
| SQLite regressions in the final full-suite TRX | 22 passed: six existing tests and 16 new migration cases. |
| `scripts/run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive` | `PASS: 380 executed tests passed; 0 skipped.` Live Smoke excluded. |
| `scripts/run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive` | Release build succeeded; 0 warnings, 0 errors. |
| `scripts/run.ps1 -Task Format -NoRestore -NonInteractive` | `PASS: Verifica formato completata.` |
| Read-only `sqlite3_libversion_number` probe of the test output's win-x64 native DLL | `3053003` (SQLite 3.53.3); the upstream WAL-reset fix is already included, so no dependency update is needed. |

Regressions migrate real SQLite fixtures with WAL and rollback journals; preserve preset/item IDs, same-ID/different-source packages, active/other metadata and unrelated tables; retain unique/FK constraints; reload/save through the current store; and restore the full old snapshot through native BackupDatabase, including the retired values. A late indexed-column failure proves the earlier three removals roll back and the schema stays version 0; repair/retry succeeds without overwriting the first backup. Backup filename I/O failure, cancellation before migration, unknown/current/future schemas and concurrent store initialization are covered. New migration fixtures clean up their own files and directories.

Updated canonical architecture, operational recovery instructions, repository guidance and the project-skill persistence reference. Removed the completed SQLite residue from the tracker; **12 pending entries remain**. The retained earlier setup/portable artifacts do not include this source change and must be regenerated by the release gate before shipping. Personal-store migration, actual-profile downgrade and physical power-loss qualification were not performed; repository-only authorization is preserved. No commit/push or cleanup retry occurred in this migration continuation.

Primary sources: [SQLite user_version](https://www.sqlite.org/pragma.html#pragma_user_version), [DROP COLUMN contracts](https://www.sqlite.org/lang_altertable.html#alter_table_drop_column), [Microsoft native backup](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup), [SQLite backup locking](https://www.sqlite.org/c3ref/backup_finish.html), [transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions) and [WAL persistence](https://www.sqlite.org/wal.html#the_wal_file).

## Session handoff — requested save on main

The user requested saving all current work, committing on the default branch and pushing before changing session. Verified the local branch and remote default branch are `main`; `git fetch origin` succeeded and the pre-save `HEAD...origin/main` divergence is 0/0. This save includes the native SQLite migration, reduced entities/store writes, 16 new migration cases, connection-string handling, corrected README/operations guidance, canonical documentation and tracker updates. No runtime source changed after the final 380-test, zero-warning/error Release build and format verification.

Resume from [PROJECT_STATUS.json](../PROJECT_STATUS.json): **12 actual pending entries**, plus the current session-context entry. SQLite migration and documentation consistency are complete and removed from pending work. User authorization remains repository-only: no isolated Windows environment is available. Do not launch the normal app or infer approval to migrate/restore the personal database from this save request; startup of the new build can apply schema migration. Retain legacy/secret files, personal stores, ignored artifacts and verified database backups. A genuine previous installer/payload and actual deployed landing URL are still missing. Live Windows Update, real WinUI/picker/log-viewer, hosted release, true upgrade, long-idle and other-profile cleanup qualifications remain open.

Current evidence: 380 executed offline tests pass, including 22 SQLite cases; Release compilation has zero warnings/errors; format, local documentation links, JSON/task uniqueness, UTF-8/CRLF and diff checks pass. Native SQLite reports 3.53.3. No dependency/lockfile changes, personal migration, live operations or packaging rerun occurred. Earlier ignored setup/portable/landing downloads do not contain this migration and must be regenerated before shipping. Full Check remains unrun because its cleanup removes retained outputs. The exact skill-validation fixture remains blocked by automatic execution review (`blocked by policy`); do not bypass the rejection or delete unrelated scratch/evidence.

The current handoff is included in the save commit. On resuming, identify that commit through `git log`, verify its remote equality and checkout state, and use the current tracker instead of historical counts. Commit/push success and the resulting SHA are verified after execution and reported to the user; this document does not predict their outcome.
