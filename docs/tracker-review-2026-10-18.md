# Local CI integration and preset UI validation — 2026-10-18

The user authorized the proposed local integration and tracker alignment. Starting `main` and remote `main` both resolved to `80253167f52327551ac4383833a97ae375c3c48e`. Local and remote `codex/ci-restore-diagnostics` both resolved to `52ddde918ffb92e5afb675700198e00b399d2a1e`.

`git merge --ff-only codex/ci-restore-diagnostics` brought the four existing commits into local `main`: 17 files, including the SDK/lockfile alignment, retained gate diagnostics, explicit hosted NSIS provisioning, regressions and the [2026-10-17 qualification record](tracker-review-2026-10-17.md). No implementation was duplicated and no new Git commit or push was performed. Remote `main` was not changed.

The exact integrated commit has a successful hosted [build-gate run 39066761011](https://github.com/dennidalpos/OnlyWinget/actions/runs/39066761011), confirmed through GitHub before integration. The prior analysis read its logs: 380 offline tests passed, builds had zero warnings/errors, and artifact upload succeeded. These are hosted results for that commit, not a new local full gate or interactive qualification.

## Local verification and preservation

| Command or check | Result |
| --- | --- |
| `git merge-base --is-ancestor main codex/ci-restore-diagnostics`, before integration | Exit 0; fast-forward available. |
| `git merge --ff-only codex/ci-restore-diagnostics` | Passed; local `main` advanced to `52ddde9`. |
| `dotnet --version` | `10.0.402`, matching the integrated exact SDK pin. |
| `dotnet restore OnlyWinget.sln --locked-mode --nologo` | Passed for all five projects. |
| `pwsh -NoProfile -File scripts/test-check-diagnostics.ps1` | Passed default/Fast/Full diagnostic cases and the real isolated NU1004 negative fixture. |
| SHA-256 preservation checks | All 91 protected local/evidence files and all five lockfiles remained unchanged. |
| Tracker JSON/schema/uniqueness and report links | Passed; ten unique pending entries, only the existing `todos` property, all relative report links resolve. |
| `git diff --check` | Passed. |

New command logs and `preservation-and-metadata.json` are retained under ignored `artifacts/integration-20261018-7073c776`. The diagnostics regression retains its three negative-fixture logs under `artifacts/check`; their expected failure does not indicate a failed main-solution restore. The full local build/test/package gate was not rerun; the exact imported commit's hosted gate is the full-gate evidence.

Before integration, the preexisting `AGENTS.md` and `.continue/` were copied to `C:\Users\Utente\AppData\Local\Temp\OnlyWinget-integration-7073c776d2d44edc86d6b35f6ff70bb5`. That directory also contains the original/target commit IDs and a SHA-256 manifest covering those local files plus historical `artifacts/ui-tracker-20261009` and `tmp/ui-tracker-20261009` evidence. No cleanup gate, personal-store operation, live package/update operation or application launch was run.

## Handoff after local CI integration

At that handoff, AUDIT-19 remained implemented with interactive validation pending. The planned cases from the [UI handoff](tracker-review-2026-10-09.md) were source-only and same-ID/different-source edits, cancellation during mutation, unchanged drafts, tracker/preset/mode navigation, system close and reentrancy in an isolated fixture. Completion required actual UI assertions and persisted state across save/restart; cancellation/failure must retain the draft or accepted-unsaved state, and save retry must not repeat an accepted mutation.

At that handoff the tracker kept its existing `todos` schema and ten pending entries. CI restoration stayed closed. AUDIT-03/04 still needed suitable updates; the zero-update inventory was from 2026-10-09. AUDIT-12 still needed hosted release-prevention qualification; build-gate success was insufficient. The genuine previous installer, deployed landing URL, AUDIT-22 UI cases, long-idle observations, affected-profile ownership and historical scratch cleanup remained outstanding. No additional implementation or qualification was inferred from the integration alone.

## Continuation — AUDIT-19 closed

The user subsequently requested continuing with the pending preset UI validation. WinApp 0.7.1 is available. Its sandbox target reports `sandbox_setup_required` / `FeaturePayloadMissing`; no Windows feature was enabled. All app data paths previously used the personal profile, so a shared `AppDataPaths.Root` now supports the explicit process environment variable `ONLYWINGET_DATA_ROOT`. Database, legacy import, settings, source preferences and diagnostic logs use the same root. Relative/blank explicit roots fail instead of falling back; the ordinary unset-variable default is unchanged. The directory is resolved once per process. [Operations](operations.md) documents its scope and the [.NET path contract](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.ispathfullyqualified?view=net-10.0).

Actual Release WinUI executions used the isolated directory `artifacts/ui-audit19-20261018/profile`, seeded with two synthetic presets and a fixture-owned legacy workspace. A locally compiled, process-scoped `winget.exe` fixture supplied two distinct sources and deterministic metadata; one package lookup waited for cancellation. It logs commands and rejects unimplemented operations. This is real WinUI/SQLite qualification with a controlled CLI boundary, not qualification of live WinGet or actual package installation. No personal directory was swapped and no global environment variable was changed.

| Remaining scenario | Actual result and evidence under `artifacts/ui-audit19-20261018` |
| --- | --- |
| Unchanged rename/package drafts | Activity navigation proceeds without confirmation; `unchanged-rename-navigation.json`, `unchanged-package-navigation.json`. |
| Dirty draft and light dismissal | Reopening retains its text; navigation Cancel retains the edit/current page; `guard-cancelled.json`, `draft-after-light-dismiss.json`. |
| Package mode and preset navigation | Search mode is guarded; Discard proceeds without saving the draft. Preset switch Cancel preserves Alpha in storage; `mode-guard.json`, `mode-after-discard.json`, `preset-switch-guard.json`. |
| Source-only replacement | SQLite contains exactly one `Fixture.App` identity with the replacement source; `source-edit-db.json`. |
| Same ID/different sources and exact duplicates | Both source identities persist; an exact duplicate disables Save. Two rows survive restart; `same-id-two-sources.json`, `duplicate-package-rejected.json`, `restart-alpha-two-sources.json`. |
| Asynchronous mutation/cancellation | Fields lock, navigation stays on the preset page, and system close cannot abandon the operation. Cancellation preserves an editable `Slow.App` draft and stores no package; `mutation-in-flight.json`, `close-during-mutation.json`, `mutation-cancelled.json`. |
| System close/reentrancy | Repeated close retains one confirmation; Cancel retains ID/source. Apply saves a new preset exactly once and then closes; `system-close-reentrant.json`, `draft-after-close-cancel.json`, `apply-on-system-close.json`. |
| Save/restart | New process reloads `PersistedFromClose` as its active UI selection; stored rows retain both sources and omit the cancelled package. An unchanged workspace closes normally; `restart-selected-preset.json`, `final-fixture-db.json`, `restart-alpha.png`. |

`assertions.jsonl` retains 27 successful assertions, including fixture shutdown and preservation checks. One earlier assertion incorrectly required an in-flight edit flyout to remain open after navigation; navigation legitimately light-dismisses it while retaining the pending draft and current page. The failed assumption remains in the file and is superseded by the subsequent page/locked-control assertion and cancellation/reopen check. This is not presented as an all-pass initial run. Other interrupted automation attempts exposed a refused foreground input and duplicate/stale popup selectors. Scoped UIA actions, observed owned popup HWNDs and refreshed selectors resolved those harness issues; no global keyboard dismissal or product workaround was introduced.

The earlier accepted-mutation/save-failure/retry qualification remains in the [2026-10-09 handoff](tracker-review-2026-10-09.md). Together with the executed remaining cases above, AUDIT-19 meets its recorded completion criteria and is removed from the pending-only tracker. The tracker now has nine entries; AUDIT-22 is the next functional UI activity and remains unqualified for its outstanding cases.

Validation after the path change:

- Release Typecheck via `scripts/run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive`: passed, zero warnings/errors.
- Targeted `AppDataPathsTests`: five passed, including explicit invalid-root rejection.
- `scripts/run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive`: 385 executed/passed, zero failed/skipped; four live Smoke tests excluded. Logs and a copied TRX are retained with this UI evidence.
- `scripts/run.ps1 -Task Format -NoRestore -NonInteractive`: passed.
- Final tracker schema/uniqueness, maintained Markdown links and `git diff --check`: passed. `final-summary.json` records nine remaining entries, 168 unchanged personal files, 91 unchanged historical/local protected files and zero running fixtures.
- Reviewed the changed C# paths against `winui-code-review`: no binding, dialog, theme or workflow mutation changes; both UI settings/logging and Infrastructure stores consume the shared Application path boundary. Default paths and legacy-file preservation remain intact.

All three fixture instances closed gracefully. The personal root still contains the same 168 files with identical SHA-256 hashes. Only synthetic app data was read/written by the fixture. Historical UI/scratch evidence and the preexisting `AGENTS.md`/`.continue` changes remain preserved. No live package/update operation, full packaging gate, commit or push was performed in this continuation.

## Continuation — AUDIT-22 closed

The user authorized proceeding with the proposed AUDIT-22 plan. The current working source was compiled with `scripts/run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive`: passed, zero warnings/errors. No production-code defect was reproduced, and no C#/XAML or package change was needed for this qualification. The existing uncommitted app-data isolation work remains intact.

Actual WinUI executions used the new process-scoped root `artifacts/ui-audit22-20261018/profile`, English/Light settings, and the previous fixture's controlled CLI executable. Only synthetic app data was written. Evidence and reproducible session fixtures are retained under `artifacts/ui-audit22-20261018`; the old native High Contrast/clipboard scratch helper was not reused.

| Remaining scenario | Executed result and retained evidence |
| --- | --- |
| Copy success and formatting | Filtered the actual viewer to one startup entry. The clipboard held exactly one formatted timestamp/level/caller/message line; UI reported `Displayed log entries copied.`. `copy-filtered.json`, `copy-success.json`, `clipboard-captured.json`, `copied-fixture-logs.txt`. |
| Copy failure and retry | A separate STA helper held the clipboard open without clearing it. The actual UI reported copy failure; the log recorded COMException `0x800401D0`. After releasing the lock, copy succeeded and the original displayed entry remained. `copy-failure.json`, `copy-retry.json`, fixture diagnostic log. |
| Export I/O failure and retry | Selected an existing fixture-owned log destination through the real save picker and confirmed overwrite while a read handle denied writes. The application reported IOException, preserved the sentinel file and displayed log, and retained diagnostics. Retrying to a new fixture path exported exactly the filtered clipboard text. `export-failure.json`, `export-retry-corrected.json`, `locked-export.log`, `retry-export.log`. |
| Retained failure diagnostics | Both copy and export failures remained in the actual viewer buffer and the isolated disk log. `retained-failure-diagnostics.json`, `profile/logs/onlywinget-20261018.log`. |
| Native picker cancellation and normal closure | Verified the picker ownership chain to the fixture main HWND, cancelled only that picker, observed `Log export cancelled.` with the displayed log intact, closed the viewer, then closed the app normally. The monitored process exited with code zero within five seconds. `native-picker-cancelled.json`, `native-picker-cancelled.png`, `cancel-then-close-owned-windows.json`, `cancel-then-close-exit.json`. |
| Owner shutdown during native picker | UIA confirmed the native modal picker disabled its owner. Posting WM_CLOSE only to the verified main HWND closed the app and its owned picker within five seconds, with exit code zero and neither window remaining. `close-owner-with-picker-qualified-before.json`, `close-owner-with-picker-qualified-owned-windows.json`, `close-owner-with-picker-qualified-exit.json`. |

The old five-second shutdown probe established only its observed behavior. The new exact-HWND close qualification supersedes the broader assumption that the owner necessarily stays open until picker cancellation. This does not claim that clicking a disabled owner is possible, or that CancellationToken cancels the native picker API: FilePickerService still passes its token only to file I/O. Normal picker cancellation and owner-window teardown were tested separately on the manifest's Windows App SDK 2.3.1 build. The official [picker specification](https://github.com/microsoft/WindowsAppSDK/blob/main/specs/Storage.Pickers/Microsoft.Windows.Storage.Pickers.md) describes owner association; actual shutdown behavior is supported here by the recorded execution, not inferred from that current specification. Clipboard contention follows the documented [OpenClipboard contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openclipboard), and the UI assertions use [Microsoft UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32).

The session preserves unsuccessful harness attempts rather than claiming an all-pass initial run. One assertion expected the invented text `Logs exported.`; the actual localized text is `Displayed log entries exported.`. That incorrect assertion remains in `assertions.jsonl`, superseded by the successful exact-text assertion and the byte-for-byte export comparison. A second incorrect final assertion treated any process reusing a fixture PID as a surviving fixture: PID 12900 had subsequently been reused by an unrelated WebView process. Executable identity and creation time established that the clipboard helper had exited; the unrelated process was not touched, and the corrected assertion checks ownership. Initial clipboard preservation attempts stopped before mutation because the managed clipboard API exposed the empty `EnterpriseDataProtectionId` metadata format without a payload; the final helper preserved that null marker and verified it during restoration. Missing optional UI-tree properties, a selector-less property query and refused foreground input were harness issues, resolved with guarded tree reads, UIA owner-state inspection and verified focus. One extra session-owned app instance was launched after an interrupted UI command and immediately closed gracefully with exit code zero; its `close-owner-with-picker-exit.json` is not the qualified picker-shutdown run. An earlier unmonitored exact-HWND close also exited; only the subsequent monitored run supplies the exit-code qualification.

The completion criteria are met together with the prior theme/clear/export checks: copy and export expose real success/failure/cancellation outcomes, failures retain displayed entries and diagnostics, retries succeed, and verified native-picker cancellation and owner teardown leave no owned window behind. AUDIT-22 is removed from the pending-only tracker, which now has eight unique entries. Current references are aligned; historical reports remain dated records.

Preservation readback verifies all 168 personal files have unchanged SHA-256 hashes and the same file count, and all 182 protected files under the earlier UI/integration evidence directories remain unchanged. The original clipboard text and all three original formats, including the null metadata marker, were restored and checked in the helper; no personal clipboard payload was written to evidence. All session-owned app/helper processes have exited. `runtime-summary.json` and `final-summary.json` retain the results.

Only documentation, skill handoff notes and the existing tracker were changed by this continuation. The SDK remains 10.0.402. No new offline suite, live WinGet/Windows Update operation, full packaging gate, installer transition, commit or push was performed. Remaining work is AUDIT-03/04, AUDIT-12, AUDIT-31, deployed landing qualification, long-idle observation, affected-profile protocol ownership and the historical scratch-cleanup residual.

Final checks: `scripts/run.ps1 -Task Format -NoRestore -NonInteractive`, tracker JSON/schema/eight-entry uniqueness, maintained Markdown links and `git diff --check` passed. `assertions.jsonl` contains 29 successful assertions and the two superseded harness assertions described above. Runtime qualification applies to the current local binary/source hashes in `qualification-manifest.json`, not to the hosted commit's staged setup/ZIP.

## Continuation — AUDIT-12 local preparation; hosted execution pending

The user requested proceeding after AUDIT-22 closure. The next executable priority is the remaining hosted release-prevention qualification. Live readback confirms that the public `dennidalpos/OnlyWinget` repository has no remote tags, zero releases and zero retained `release.yml` runs. The existing workflow is registered on default `main`, and the remote branches remain `main` at `8025316` and `codex/ci-restore-diagnostics` at `52ddde9`. No new remote write or dispatch was made.

A managed worktree is attached at `C:\Users\Utente\.codex\worktrees\audit12-hosted-qualification\OnlyWinget`, on local branch `codex/audit12-hosted-qualification`, starting from `52ddde918ffb92e5afb675700198e00b399d2a1e`. The primary checkout's pending app-data/AGENTS/editor/UI work was preserved and not copied into this fixture. Only three files are prepared for the future fixture commit: `.github/workflows/release.yml`, `scripts/test-hosted-release-fixture.ps1` and `docs/audit12-hosted-qualification.md`. This is a branch-only fixture and must not be merged into production.

The fixture removes automatic tag-push invocation and grants its GITHUB_TOKEN only `contents: read`. It checks out the dispatch SHA without persisted Git credentials, creates an annotated tag only in that runner's checkout, and executes the unchanged production release validator/full Check command/final validation/publisher with their original failure conditions. For the gate-failure case only, it inserts the incompatible `net10.0/win-x64` target into the Domain lockfile before the real Check command. The malformed lockfile and test tags are never committed or pushed. No production workflow change is made in the primary checkout.

The local regression extracts both setup blocks from the actual fixture YAML, verifies manual-only/read-only configuration, pinned checkout, guard ordering and identical production commands/publisher, then executes the real negative paths in fresh clones. The tag-mismatch case creates existing tag `v1.0.999997` and receives the actual version-mismatch rejection. The gate-failure case first passes exact tag/version validation, then the unchanged Check command fails at restore with retained NU1004 diagnostics and a `FailedStep: restore` report. These are expected fixture failures, not failures of the unchanged solution's restore. GitHub step-skipping remains unqualified.

| Executed check | Result |
| --- | --- |
| `dotnet restore OnlyWinget.sln --locked-mode --nologo` in the new worktree | Passed. |
| `scripts/test-hosted-release-fixture.ps1 -OutputDirectory <new evidence directory>` | Both actual negative cases rejected, production command/publisher comparisons and fixture safeguards passed. Final-code readback passed under `../evidence/local-negative-cases-final`; the initial run is also retained. |
| `scripts/test-release-validation.ps1` | Passed the existing refs/commits/versions/assets/tag-drift cases. |
| `actionlint 1.7.12 .github/workflows/release.yml .github/workflows/build-gate.yml` | Passed, including after pinning the dispatch SHA. |
| `scripts/run.ps1 -Task Lint -NonInteractive` | Passed, 41 scripts. The initial run flagged assignment to the automatic Matches variable; renaming to stepMatches resolved it. Final edited-script lint also passed. |
| `dotnet test --nologo` | 380 passed, zero failed, four live tests skipped (384 total). This qualifies the base commit in the fixture; the primary checkout's separate AppDataPaths tests are not present here. |
| `dotnet build -c Release --nologo` | Passed, zero warnings/errors. |
| Diff whitespace, fixture guide links, unchanged production-script hashes and pending-only tracker schema | Passed. The prepared branch contains exactly the three fixture files; the primary tracker retains eight unique strings in its existing todos array. |

The concrete hosted plan is documented in the fixture guide: after explicit commit/push authorization, publish only `origin/codex/audit12-hosted-qualification`, let its normal build-gate run, then dispatch existing `release.yml` on that branch with `qualification_case=tag-mismatch` and `qualification_case=gate-failure`. The repository's default workflow registration allows [dispatching a selected branch](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow). The [token permission contract](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token) and [release API requirements](https://docs.github.com/en/rest/releases/releases#create-a-release) provide a separate no-publication boundary; they are not substituted for checking that the publisher is actually skipped.

Completion requires matching run/artifact/checkout SHAs; mismatch failure at initial validation with subsequent gate/publisher skipped; gate-failure initial validation/prerequisites succeeding, actual Check failing with NU1004, final validation/publisher skipped and diagnostics uploaded; and unchanged remote tag/release inventories. A denied publisher attempt or an unrelated workflow failure is not a pass. No positive publication is proposed. AUDIT-12 stays open and the tracker retains eight pending entries.

No commit, push, hosted dispatch, remote tag or release was performed. The repository's current user-provided instructions explicitly prohibit automatic commit/push, so authorization for the prepared fixture branch and dispatches is the remaining decision. The existing production release workflow, application sources, primary historical evidence and all other pending work are preserved.

## Continuation — AUDIT-12 hosted negative qualification completed

The user explicitly authorized the prepared fixture commit/push, its branch CI and both negative release dispatches. Before committing, the exact three prepared file hashes matched the manifest from the passing local checks; staged scope and whitespace were verified. Commit `3f741468fdfcfd8728fc540c610560750e49739f` (`test: qualify hosted release rejection paths`) contains only the fixture release workflow, its local regression and guide. It was pushed only to `origin/codex/audit12-hosted-qualification`. Local primary `main` remains `52ddde9`; remote `main` remains `8025316`. No PR or merge was created.

Both `gh workflow run release.yml --repo dennidalpos/OnlyWinget --ref codex/audit12-hosted-qualification -f qualification_case=<case>` calls returned concrete run URLs. Independent run/job/log/artifact readback verifies the exact published SHA and each required step outcome:

| Case | Hosted execution and verified result |
| --- | --- |
| Explicit tag/version mismatch | [Run 39119700007](https://github.com/dennidalpos/OnlyWinget/actions/runs/39119700007): checkout and runner-only annotated tag preparation succeeded. The real validator rejected `v1.0.999997` with `Release tag/version mismatch: Version must be 1.0.999997.` SDK setup, NSIS, injection, full Check, final asset validation and `Create GitHub Release` were all skipped. Diagnostics upload succeeded. |
| Failed full gate prevents publication | [Run 39119705929](https://github.com/dennidalpos/OnlyWinget/actions/runs/39119705929): runner-only `v1.0.2`, initial real validation, SDK setup, NSIS and injection succeeded. The unchanged full Check entrypoint failed at the actual locked restore with NU1004. Final SHA/assets validation and `Create GitHub Release` were skipped; diagnostics upload succeeded. |
| Ordinary branch CI | [Build-gate run 39119692521](https://github.com/dennidalpos/OnlyWinget/actions/runs/39119692521): succeeded on the same SHA. Independent log/job readback confirms all 21 gate stages, 380 executed/passed offline tests with zero skipped, 41 linted scripts, zero compiler warnings/errors and successful artifact upload. Four live Smoke tests were excluded. This is separate from the two intentionally failed release runs. |

The expected `failure` conclusions of the two release runs are successful negative qualifications because their precise guard failures and skipped publisher were checked. Neither run attempted publication. Actual runner logs show `Contents: read`; uploaded identity files show the same case, project version, local tag and checkout SHA. The setup tags existed only inside the respective runner checkouts. The production release guard commands and publisher/default success conditions are unchanged, as established by the local regression; the fixture's permission reduction is an additional boundary, not the reason the publisher step was skipped.

Artifact `onlywinget-release-verification` was downloaded and independently inspected for each run:

| Run | Artifact ID | GitHub-reported archive digest | Readback |
| --- | --- | --- | --- |
| 39119700007 | 12133796970 | `sha256:5726813ae8a79d3a0c995cb387d0f2d9f0ba94bfc01dfb8ff8a4be03a56d16d5` | One identity JSON, with exact case/source/tag/version and read-only token declaration. |
| 39119705929 | 12132769900 | `sha256:f2b1c46c076079c1a24114f8a929742fe0f41e0690209c1d023e44bbdfbef5d9` | Identity/injection JSON, real `restore.log` with NU1004 and `build-report.txt` with `Result: FAILED` / `FailedStep: restore`. Original/injected lockfile hashes differ. |

The original ZIPs were also downloaded through GitHub's [artifact API](https://docs.github.com/en/rest/actions/artifacts#download-an-artifact); independently computed SHA-256 values match both GitHub-reported archive digests. `archive-verification.json` retains each comparison. Extracted file sizes and independently computed SHA-256 values are retained in each `proof.json`. All API snapshots, raw/ANSI-cleaned run logs, original ZIPs, extracted artifacts and proof manifests are retained outside the managed checkout at `C:\Users\Utente\.codex\worktrees\audit12-hosted-qualification\evidence\hosted-3f74146`. `verify-hosted-release.ps1` reproduces the readback assertions. It locates unique evidence filenames recursively because uploading runner-temp and workspace paths retains their common-ancestor directory layout.

Post-run `git ls-remote --tags origin` and authenticated release inventory remain empty, matching the before-state. The qualifier branch is the only added remote ref. No remote tag, release, publication or installed-app/package operation was performed. The primary checkout's previous qualified source/binary hashes still match; its app-data work and ignored UI evidence were not included in the fixture commit or cleaned. The branch remains a disposable qualification fixture and must not be merged into `main`.

The ordinary build-gate uploaded `onlywinget-build-gate`, artifact ID `12133922406`, 163,587,963 bytes, GitHub-reported digest `sha256:eb6d95ab606fa336dc75fd00df895b9e724b8d9236a8ae7cb069166ed2a5c62b`. Its metadata, full logs and independent gate-result assertions are retained under `hosted-3f74146/39119692521`. This continuation did not download or launch its setup/portable payload; packaging and payload analysis are the runner's qualified gate results. The fixture's 380-test suite is the base application source, not the primary checkout's separate uncommitted 385-test app-data change. GitHub also emitted a non-blocking Node.js 20/forced-24 annotation for checkout/setup-dotnet/upload-artifact; the jobs completed under that runner policy, and no action upgrade was included.

AUDIT-12 now meets the recorded hosted negative completion criteria and is removed from the pending-only tracker, leaving seven unique entries. The tracker retains its existing todos schema. Maintained release documentation and the packaging skill point to this qualification. The earlier local-preparation section remains a dated record and is superseded by these run results. Tracker/report/release-documentation/skill alignment remains uncommitted in the primary checkout; the authorization committed and pushed only the three fixture files on the dedicated branch.

This qualifies the two recorded hosted negative prevention scenarios. It does not qualify positive release publication or replace shipping checks on a future genuine release tag. No broader commit, main push, tag, release, PR, merge or live update/install was performed. Remaining work is AUDIT-03/04 live Windows Update qualification, AUDIT-31 genuine previous-installer transition, deployed landing verification, long-idle observation, affected-profile protocol ownership and the historical scratch-cleanup residual.

## Continuation — sequential remaining-work review and remediation

The user authorized proceeding through all remaining items in order. This continuation preserves the existing uncommitted app-data isolation, editor instructions and prior qualification work. Local `main` remains `52ddde9`; no commit, push, tag, release, workflow dispatch or website publication was requested or performed. Evidence is retained under ignored `artifacts/resume-all-20261018`.

### Windows Update: implemented corrections and actual qualification

The read-only default software search at 13:45 Europe/Rome on 2026-10-18 returned `ResultCode=2` and one available, not-downloaded Defender update: `3a7ecf68-0ecc-4202-95e1-715c0765c441`, revision `200`, KB2267602, intelligence version `1.461.88.0`. This supersedes the old zero-update inventory for that observation time; availability remains volatile. No EULA was accepted and no download/install was requested by the inventory.

Source inspection found a remaining AUDIT-03 defect: the PowerShell fallback still called synchronous `Search`, `Download` and `Install`, while process cancellation killed its worker without requesting WUA abort. It now uses Begin/End jobs and a managed COM callback that signals an event without entering a PowerShell runspace. A unique named event passes caller cancellation/operation timeout to the worker; the worker requests abort and calls `CleanUp` outside callbacks. The host waits for worker exit instead of immediately cancelling the process runner. Abort/cleanup failure or expiry of the 30-second grace returns an explicit uncertain-state error, without automatic retry. This cannot guarantee rollback or immediate native cancellation. The forced-stop deadline branch was reviewed but was not exercised against a real update job.

Both installation paths previously accepted EULAs while still discovering matches. Acceptance now follows complete ID/revision coverage validation. PowerShell also rejects an unsuccessful search result before selecting/downloading updates. The controlled production-script regression verifies that an available matching update combined with a missing revision cannot accept its EULA or create a downloader; an unsuccessful search is rejected at the same boundary.

Seven new executed Windows PowerShell worker/script regressions cover successful completion, End failure, active cancellation, operation timeout, native cleanup failure, incomplete selection and unsuccessful search. These use fake jobs/COM boundaries and do not modify Windows Update. The first three-test run failed because the fixture's `$job` variable was shadowed by the helper's local variable. Renaming it to `$fixtureJob` fixed the harness; `powershell-jobs-initial.trx` preserves that failed attempt. The final targeted run passed 7/7.

Three existing live read-only tests passed: native search callback completion/abort/cleanup, PowerShell discovery, and COM-or-fallback discovery. A separate retained console harness then called the actual COM and PowerShell installation entrypoints with a deliberately noninstallable selection: the observed ID at revision `int.MaxValue`, plus a fresh nonexistent ID. Both returned failure with zero rows and no download/install progress. This qualifies actual rejection before mutation; it does not establish a genuine revision transition between scans or positive installation. `live-selection.log` retains both results. Two initial console-harness builds failed on its relative project-reference path and fixture-only analyzer/import configuration; their logs are preserved, and production manifests/analyzer settings were not relaxed.

These changes follow Microsoft's [asynchronous WUA guidelines](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/guidelines-for-asynchronous-wua-operations) and [RequestAbort contract](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iinstallationjob-requestabort). Real download/install cancellation and positive exact-revision installation still require an identified restorable Windows environment. None was identified in this session; no Windows feature was enabled and the available personal-machine update was not installed.

### Remaining items and completion boundaries

| Priority / item | Verified outcome | Remaining completion criterion |
| --- | --- | --- |
| P1 AUDIT-03 | Fallback lifecycle corrected; controlled worker regressions and actual search cancellation/discovery pass. | Real download/install cancellation, native outcomes and restoration evidence on suitable updates. |
| P1 AUDIT-04 | EULA acceptance deferred; controlled incomplete coverage and actual negative selection pass in both live paths. | Successful complete exact-identity installation and an actual revision-transition case. |
| P2 AUDIT-31 | Actual NSIS ownership/protocol and exact-process closure regressions pass. Static inspection found that the local ZIP named `1.0.1` contains `OnlyWinget.dll` version `1.0.2.0`, informational version `1.0.2+c7864369d87716826d52f9e178abb1a063b11860`. Its name is not proof of an earlier release. No old installer was executed. | Authentic prior payload and controlled installation environment; design/qualify obsolete-file reconciliation and preservation of unrelated files. This source work remains open. |
| P3 AUDIT-30 | Found and corrected drift: prepared landing downloads from October 8 differed from `dist` from October 10. Preserved both old pairs, built the current source and copied its verified pair with the existing helper. Landing regression and actual pair hashes pass. Pages API and the repository Pages URL return HTTP 404. | Supply/identify the actual deployed URL, then verify deployed HTML and both download hashes against the intended qualified release. No deployment is inferred from local output. |
| P3 long-idle | Read-only logs contain five mitigation triggers from October 12–17 and no `MainWindow.RefreshAfterLongIdle` exception entries. | Capture a real rendering incident and its conditions; reproduce/correct/retest. Trigger logs do not prove rendering recovery, so no speculative UI change was made. |
| P3 AUDIT-37 | Current-user exact legacy key is absent. Managed ownership tests and actual NSIS tests preserve unrelated copies/labels/markers. | An identified affected profile/portable owner for scoped review; no other profiles or handlers were enumerated or removed. |
| Closed scratch cleanup | Verified four ordinary exact files, copied and hash-checked them under `scratch-recovery`, removed only those files and their empty original directory. This execution completed; the earlier rejection remains a historical fact. | Met: four recoverable identical copies exist and `tmp/ui-tracker-20261009` is absent. Removed this completed entry from the pending-only tracker. |

The tracker retains exactly six unique pending strings and only its existing `todos` property. Each entry now records current evidence and explicit completion conditions. The older AUDIT-19/22/12 closures remain intact; there is no newly inferred positive release or interactive UI qualification for this binary.

### Verification and preservation

| Command / readback | Result |
| --- | --- |
| Targeted `PowerShellWindowsUpdateJobTests`, Release/no-restore | 7 passed, zero failed/skipped; final TRX retained. |
| Three selected live WUA search/cancellation tests, Release/no-build/no-restore | 3 passed, zero failed/skipped. Separate from offline results. |
| Retained `live-validation/WuaQualification.csproj` | Actual COM and PowerShell rejected wrong-revision/nonexistent selection; no download/install started. |
| `scripts/run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive` | 392 executed/passed, zero failed/skipped; four live Smoke tests excluded. Copied TRX retained. |
| `scripts/run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive` | Passed, zero warnings/errors. |
| `scripts/test-installer-owned-files.ps1`, `scripts/test-process-ownership.ps1`, `scripts/test-landing-artifacts.ps1` | Passed actual isolated NSIS ownership/closure and landing sentinel regressions. |
| `scripts/run.ps1 -Task Format -NoRestore -NonInteractive` | Passed. |
| `scripts/package.ps1 -Configuration Release -NoRestore -Fast -NonInteractive` | Passed; build zero warnings/errors, NSIS and portable pair validation/promotion succeeded. No installer lifecycle or installed-app launch was executed. |
| `Copy-CurrentLandingArtifactPair` plus independent SHA-256 comparison | Both prepared downloads equal the newly generated distribution pair. |

The new setup is 67,128,371 bytes, SHA-256 `96637F3E1FED82108279D35018C46D3FCD948BD73987448478C4ED7F8A8D7707`; the portable ZIP is 96,912,713 bytes, SHA-256 `A7B38A31163035354E9A92DC80B9E35F6596E964969EFB42863688A8C5C307D3`. These are local uncommitted-source artifacts at the unchanged project version 1.0.2, not a tagged release. `packaging-before` retains both previous distribution/landing pairs and stage metadata; the previous portable ZIP preserves the previous published payload. Old files labelled 1.0.1 were inspected statically and preserved.

Preservation readback matches all 168 personal files by count and SHA-256, all 236 historical/local evidence files in place and the four intentionally relocated scratch files in recovery. No personal profile was swapped, no normal app instance was launched, and no unrelated process or registry handler was stopped/deleted. `qualified-source-hashes.json`, preservation manifests, TRX, command logs, inventory and artifact hashes retain reproducible boundaries. The root `Check` cleanup wrapper was not run because it removes retained ignored evidence; the individual checks above do not constitute a full shipping/hosted gate. Commit/push, true upgrade, positive publication, live download/install and the remaining environment-dependent cases were not performed.

Final readback also passes script lint (40 scripts), an independent `Assert-PackageArtifactPair` against the complete published payload, relative maintained-document links, six-entry tracker schema/uniqueness and `git diff --check`. All four tested/packaged WUA source hashes still match; `source-snapshot.json` records HEAD plus the wider source/configuration snapshot. Thirteen preexisting modified/untracked files outside the intentionally updated tracker/operations/handoff remain byte-for-byte identical to their before-copies. No broader instruction/editor/app-data changes were folded into this continuation. SecondBrain coordination was registered for the repository dossier; no dossier or private IDE memory change was needed.

## Decision — Windows test environment tasks cancelled

The user subsequently confirmed that no Windows test environment is available and instructed cancelling those tasks. Removed AUDIT-03 and AUDIT-04 from the pending-only tracker: their remaining scope was live download/install cancellation, positive installation and a genuine revision transition in a restorable Windows environment. Cancelled the environment-dependent true-upgrade qualification portion of AUDIT-31 as well. These cases were not executed or passed; their existing implementation, controlled regressions and historical evidence remain preserved.

AUDIT-31 retains only the open obsolete-file reconciliation source work, including its authentic-prior-payload dependency. Deployed landing verification, real long-idle observation and affected-profile protocol ownership remain outside this cancellation. The tracker now has four unique entries in its unchanged `todos` schema. This decision supersedes the earlier six-entry pending summary; it does not change product behavior, delete tests or establish release qualification.

Validation for this documentation-only decision: parsed tracker JSON/schema, checked the four-entry uniqueness/count and cancellation scope, reviewed the diff and ran `git diff --check`. Application build/tests were not rerun because no application or test source changed.

## Final handoff — tracker, dossier and pre-commit checks

The user requested tracker/SecondBrain alignment and final commit/push. The pending-only tracker keeps four entries: AUDIT-31 source reconciliation, deployed landing verification, long-idle observation and affected-profile protocol ownership. Skills and release documentation now record the cancelled Windows test-environment tasks consistently; these cases remain unexecuted and are not release qualification. The OnlyWinget SecondBrain dossier records the same decisions and points to this tracker instead of creating another task file.

Fresh mandatory pre-commit checks on 2026-10-18 passed in sequence:

- `dotnet test --nologo --logger 'trx;LogFileName=precommit-tests.trx' --results-directory artifacts/final-handoff-20261018`: 392 passed, zero failed, four opt-in live Smoke tests skipped, 396 total. Live opt-in was unset for this process only. No cancelled live qualification was rerun.
- `dotnet build -c Release --nologo`: passed with zero warnings/errors.
- `scripts/run.ps1 -Task Format -NoRestore -NonInteractive`: passed.
- Tracker schema/four-entry uniqueness/cancellation scope, maintained relative links and `git diff --check`: passed.
- SecondBrain operational `-Check`, under the registered session: 20 repositories/checkouts/directives, zero changes and zero issues. Only the curated OnlyWinget dossier is updated; unrelated project changes are preserved.

New raw logs and TRX are under ignored `artifacts/final-handoff-20261018`; earlier UI/runtime/package evidence and recovery copies remain local. The final source commit includes the app-data isolation and Windows Update corrections, regression tests, existing project-instruction alignment and maintained qualification documentation. The four previously integrated CI commits are already ancestors of local `main`; publication uses an ordinary fast-forward push to the existing public `origin/main`. The hosted release qualification fixture branch remains separate. This handoff does not publish installers, a website, tags or releases.
