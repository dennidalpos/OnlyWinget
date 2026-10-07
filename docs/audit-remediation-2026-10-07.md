# Audit remediation — 2026-10-07

Continuation of [the audit](audit-2026-10-05.md) and [previous remediation](audit-remediation-2026-10-06.md). The pending-only tracker remains [PROJECT_STATUS.json](../PROJECT_STATUS.json).

## AUDIT-16 — Closed

Snapshots and all workflow state mutations now use the same dedicated state lock, including metadata, progress, source rows, activity, workspace and available/selected lists. State cache invalidation occurs within each mutation. Search/update fan-out collects local outcomes and publishes rows with available selection in one guarded step. Workspace and source preferences publish together only after both loads succeed; no lock spans an await.

Replaced the independent async admission flag with one operation semaphore shared by persistent synchronous preset edits and async workflows. Busy edits are rejected without changing the workspace. Requested saves wait asynchronously with caller cancellation, so a UI autosave admitted after an edit cannot be rejected merely because another workflow started. Mutation commands are disabled throughout all busy states. Save failures remain actionable; clearing failed UI fields/navigation is still AUDIT-19.

Verification: **296/296** offline test methods passed before changes; **308/308** after changes (the same five inactive live-smoke methods remain included). Added deterministic barriers for delayed load, edits during search, queued saves, cancellation while waiting, and concurrent snapshot/selection publication; eight cases cover preset commands in busy workflows. Release typecheck: **0 warnings, 0 errors**. No live source changes, package installation, personal-store access or interactive UI validation performed. The full Check wrapper is not needed for this scope and would remove retained artifacts/tmp audit evidence.

Primary Microsoft sources checked on 2026-10-07: [C# lock](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock), [SemaphoreSlim.WaitAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim.waitasync?view=net-10.0), and [cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads).

## AUDIT-17 — Closed

WinGet apply now rescans only after success and without cancellation. Refresh no longer clears batch results, and discovery publishes only after successful collection/metadata resolution, preserving old rows on failure. Results, output and diagnostics remain in the presentation results pane after successful/failed rescans. Selection changes retain scan errors.

The user approved the additive cancellation exception contract on 2026-10-07. `OperationExecutionCanceledException` derives from `OperationCanceledException`, keeps the cancellation token and captures a read-only summary of completed/cancelled results. Executor signatures are unchanged. Cancellation distinguishes an active, unconfirmed package from packages never started (zero attempts), retains prior successes/failures and retry-wait diagnostics, and never triggers another attempt. Application records the summary and Activity before propagating cancellation and removes only successful packages from pending rows. Preflight skips and validation failures are recorded as they occur, including before fail-fast or cancellation. Cancelled status has English/Italian resources. Removed the unused skipped-results collector and redundant retry-delay exception block; simplified installed-package skip decisions.

Verification: **308/308** before this task, **319/319** after it, including the same five inactive smoke methods. New cases cover mixed success/failure through successful/failed rescan and explicit retry, successful-result retention, caller/global cancellation with the real executor and a blocking fixture command runner, cancellation during validation, fail-fast validation after a skip, cancellation between commands/during a command/during retry wait, and preservation of original output/attempt counts. No real WinGet command is executed by these fixtures. WinUI automatic-rescan branching was reviewed in source; interactive UI and real native install cancellation are **not verified**.

Primary Microsoft sources checked on 2026-10-07: [cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads), [Task cancellation](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation), and [OperationCanceledException](https://learn.microsoft.com/en-us/dotnet/api/system.operationcanceledexception?view=net-10.0).

### Residual: AUDIT-35 — Active-preset selector ignores rejection and persistence

Source evidence: `PresetsViewModel.SetActivePreset` returns void, ignores the Application result and calls no autosave; `PresetsPage.OnPresetChanged` neither checks the result nor restores the selector after rejection. Application correctly rejects active-preset mutation while busy, but the control can show the rejected choice while the actual workspace retains the old preset. Accepted idle changes update only in-memory workspace unless a separate save occurs. This is distinct from AUDIT-19's pending-edit confirmation/navigation scope.

Track as P2: restore the actual selection after rejection, disable switching appropriately, save accepted changes and report save failure. Verify a busy rejected change and an accepted change across restart with isolated storage; interactive selector behavior remains unverified.

### Existing residuals retained

AUDIT-27 still covers the test runner hiding compiler diagnostics and the five inactive smoke methods reported as Passed. Both compiler diagnostics encountered while authoring the new fixtures were read via direct build and corrected; they are not production failures. AUDIT-28 still covers the Application retry method without a UI consumer; source search confirms no consumer in `src/OnlyWinget`. No duplicate tracker entries were added for these existing findings.

## Final verification

| Command | Actual output |
| --- | --- |
| `scripts/run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive` | `PASS: 319/319 unit tests passed.` TRX: 319 passed, 0 failed; five inactive smoke methods included. |
| `scripts/run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive` | Release build succeeded, 0 warnings, 0 errors. |
| `scripts/run.ps1 -Task Format -NoRestore -NonInteractive` | `PASS: Verifica formato completata.` |
| `scripts/run.ps1 -Task Lint -NonInteractive` | `PASS: All scripts linted (28 scripts OK).` |

Tracker contains only pending audit items, with AUDIT-16/17 removed and exactly one AUDIT-35 entry. Documentation links, AGENTS size limit and diff whitespace checks pass. The three pre-existing modified NuGet lockfiles retain their original diff (11 additions, 2 deletions). No commit/push was performed during remediation; retained ignored audit/linter artifacts and personal application stores were preserved.

## Session handoff

The user subsequently requested saving all work, committing on the default branch and pushing before changing session. Verified branch: `main`; remote HEAD and `refs/heads/main` matched the local starting commit `dee549ed2acbc1ab4d26e28cb6d779ad1b052c85`. The save includes this report, the tracker handoff, AUDIT-16/17 remediation and the existing generated `win-x64` lockfile sections, without changing dependency versions.

Resume with AUDIT-18, then AUDIT-19. Keep AUDIT-35 open for selector rejection/persistence. Identify the save commit through `git log`; verify local/remote equality and a clean working tree before new work. Retained ignored audit/linter evidence is preserved on disk, outside the Git commit. Environment-specific P1 validations and long-idle observation remain pending.
