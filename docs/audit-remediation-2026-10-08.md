# Audit remediation — 2026-10-08

Continuation of [the audit](audit-2026-10-05.md) and [previous remediation](audit-remediation-2026-10-07.md). Pending work remains in [PROJECT_STATUS.json](../PROJECT_STATUS.json). Starting checkout: clean `main`, `HEAD` and local `origin/main` both `3a5df0a9deb1c46c967c74c624f7238dc082a496`.

## AUDIT-18 — Closed

Preset package validation now compares the domain's case-insensitive ID/source identity and excludes only the captured identity being edited. Source changes revalidate the field immediately through a two-way binding. Add mode resets the edit target; edit mode captures it once, so later checkbox selection changes cannot redirect replacement. Removed the duplicate constructor subscription to the XAML-wired edit flyout event. The pending-edit guard uses the same package validation as the save button.

Verification: baseline **319/319**, then **321/321** offline test methods passed (five inactive smoke methods remain included). Added application regressions for same-ID/different-source additions, source-only replacement and exact duplicates; the fixtures initially omitted the custom source and assumed insertion order, both corrected to match the source-validation and workspace-sorting contracts. Release typecheck: **0 warnings, 0 errors**. WinUI bindings/handlers were compiled and reviewed; interactive flyout behavior is **not verified** and will be retained with the navigation validation work.

Primary Microsoft sources checked on 2026-10-08: [Flyout and Button.Flyout behavior](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.flyout?view=windows-app-sdk-2.0), [ContentDialog](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.contentdialog?view=windows-app-sdk-2.0), and [AppWindow.Closing](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.closing?view=windows-app-sdk-2.0).

## AUDIT-19 — Implemented; interactive validation pending

The editor now returns success only after both mutation and workspace save succeed. Failed validation/rejected mutations keep the draft. An accepted mutation followed by failed/cancelled saving keeps the editor in a save-retry state; fields are locked and retry runs only persistence. This prevents a duplicate add/rename/replacement. Discard is offered for unapplied drafts; an already applied edit must be saved before leaving. EN/IT confirmation text explains this distinction.

Flyouts open explicitly after checking the previous draft, retain dirty state after light dismissal and compare against captured original values, avoiding prompts for unchanged edits. Rename validation excludes the active preset. Sidebar/tracker navigation, package-mode changes, preset switching and system close share the pending-edit guard and reject exceptions or unavailable dialog roots. Import/removal of the active preset also checks the draft first. Selection is restored after rejected/cancelled switching; accepted selector changes still need autosave under AUDIT-35.

Verification: **322/322** offline test methods passed, including a save-failure/retry/restart regression proving that the accepted preset remains in memory, the failed save leaves stored state unchanged and one successful retry persists one preset. The WinUI path compiles without warnings/errors and was reviewed in source. Actual dialog routing, light dismissal, close/reentrancy and failed-save controls are **not verified interactively**. The normal app uses personal LocalApplicationData paths without a test override; it was not launched for destructive fixture editing. AUDIT-19 remains in the tracker solely for this validation, including AUDIT-18's real flyout behavior.

Inputs and navigation are also blocked throughout asynchronous edit application, so a second interaction cannot change or discard an in-flight draft. The workspace Save button uses the same apply-and-save path when a draft is pending.

## AUDIT-20 — Closed

Native Windows Update rows now read `MsrcSeverity` and `RebootRequired`, matching the PowerShell fallback. Missing severity stays unknown rather than being labelled Important. Native article IDs remain unprefixed; presentation handles bare/already-prefixed values with one canonical KB prefix, trimming blanks and removing duplicate formatted IDs.

Extracted the existing native mapping into one internal testable method. Removed silent metadata catches and invented ID/revision/title defaults in that scope; unreadable native metadata propagates to the existing logged scan failure/fallback handling. Native update, identity, category, category collection, article collection and update collection references are released in finally blocks, including mapping failure.

Verification: **329/329** offline test methods passed. Seven new cases execute the production native mapper with automation-shaped fixtures and the presentation mapper, covering severity values/absence, current reboot true/false, a forbidden potential-reboot property, KB formatting and unreadable-property propagation. These are deterministic metadata tests, not a real WUA download/install. Existing AUDIT-03/04 live validation remains pending. No real Windows Update operation was performed.

Primary Microsoft sources checked on 2026-10-08: [IUpdate.MsrcSeverity](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate-get_msrcseverity), [IUpdate2.RebootRequired](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate2-get_rebootrequired), [IUpdate.KBArticleIDs](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate-get_kbarticleids), and [InstallationRebootBehavior](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/ne-wuapi-installationrebootbehavior).

## AUDIT-21 — Closed (continuation)

Serilog now forwards to one diagnostics store, which owns the daily UTF-8 file writer and the separate bounded memory queue. Removed the independently configured Serilog file/debug sinks and duplicate Application/store exception delegate registrations where ILogger already logs the event. Enable/level settings are applied before Host startup and atomically on changes; Verbose/Debug are represented by the existing Verbose application level, and event timestamps are preserved. No package versions or dependency declarations changed.

Write failure preserves the original memory entry and exposes the I/O error to the viewer. Clear serializes with writes, closes each append before returning, deletes only recognized daily app-log filenames and retains the buffer if cleanup fails. Partial file deletion can occur before a later file fails; the result is failure, not a claim of complete cleanup. Activity clear/Undo now affects Activity only, preserving independent diagnostic evidence. Export reuses the existing picker service and distinguishes success, cancellation and failure in the viewer.

Verification: **329/329** baseline, **336/336** after seven isolated filesystem regressions. Tests cover disabled logging, changing levels/Verbose, concurrent writes exactly once, clearing while running and then writing again, preservation of unrelated filenames, failed write/clear and bounded filtered memory. Fixture files were removed by each test. Release typecheck: **0 warnings, 0 errors**. No personal logs/settings were read, written or deleted by test execution. Actual viewer confirmation/export remains part of AUDIT-22's interactive validation.

Primary sources checked on 2026-10-08: [Serilog configuration](https://github.com/serilog/serilog/wiki/Configuration-Basics) and [official file sink ownership/retention](https://github.com/serilog/serilog-sinks-file). The single writer retains the existing AppDiagnostics daily-append naming contract. Unlike the removed Serilog sink, it has no file-size/retention policy; this pre-existing unbounded append behavior is explicitly tracked as new P3 AUDIT-36. `Serilog.Sinks.File` and `Serilog.Sinks.Debug` are now unused package declarations; review/removal was added to existing AUDIT-28, without hand-editing lockfiles.

## AUDIT-22 — Implemented; interactive validation pending (continuation)

Added the eleven missing literal tracker/log keys in both EN/IT dictionaries, plus all viewer labels, accessible names, level badges and operation outcomes. Removed ineffective null-coalescing translation fallbacks and hardcoded badge colors. Badges now use theme resources with explicit translated severity text; no severity is conveyed only by color. Copy/export share displayed-entry formatting and report actual success/cancellation/failure. Early filter events during XAML initialization are ignored until all controls exist.

Clear uses one inline Warning InfoBar with explicit confirm/cancel buttons inside the existing ContentDialog. Only confirmation invokes cleanup; failure keeps the confirmation open and displays the I/O error. There is no nested ContentDialog. Closing the viewer cancels pending export I/O; the shared picker service itself observes its token only during file I/O, so native picker lifetime/cancellation still needs validation on an isolated profile.

Verification: **339/339** offline test methods passed. The test project compiles the actual UI `TextResources.cs` without WinUI dependencies; new tests resolve every literal C#/XAML key and dynamic log-level label in EN/IT, check dictionary parity and parse all composite formats. An initial test-only CS0103 was corrected with the missing System.Text import. Release typecheck: **0 warnings, 0 errors**; format and lint pass. Actual inline confirm/cancel, clipboard/picker outcomes and Light/Dark/High Contrast rendering are **not verified** in the running UI; AUDIT-22 remains only for these checks. No personal-log deletion, clipboard mutation or picker launch was performed during this batch.

Primary Microsoft sources checked on 2026-10-08: [dialog contract](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs). The maintained WinUI skill/gallery examples were consulted for ContentDialog and InfoBar usage.

## AUDIT-23 — Closed (protocol retired by user decision)

The user chose retirement rather than implementing URL actions. Removed the public parser/request/action types, registration interface/service, startup registration/dispatch and their obsolete parser tests. Composition now performs narrowly scoped legacy cleanup: the label, URL marker and exact quoted command must belong to the running executable. NSIS uninstall uses the same ownership rules in HKCU. Failures are logged or return a nonzero uninstall status; other copies are preserved.

Verification: **339/339** baseline; **329/329** after removing fifteen retired parser cases and adding five real isolated-registry cases. Release typecheck: **0 warnings, 0 errors**. The native NSIS ownership fixture also passes for owned, other-copy, other-label and missing-marker cases, while preserving unrelated files. The first fixture run exposed a command-literal mismatch and a registry-view inconsistency; both corrected and the rerun passed. Tests use only unique `Software\OnlyWingetTests` keys and disposable files, never the actual protocol handler. No personal registry cleanup or application launch was performed.

Residual AUDIT-37 records handlers from already removed portable copies or other profiles, requiring ownership review/manual cleanup. Primary sources checked on 2026-10-08: [RegistryKey.DeleteSubKeyTree](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registrykey.deletesubkeytree?view=net-10.0) and [NSIS scripting reference](https://nsis.sourceforge.io/Docs/Chapter4.html). Installer process shutdown remains AUDIT-25.

## AUDIT-24 — Closed (continuation)

Both artifacts now build in a unique destination-volume staging directory. Before promotion, the setup is checked for nonempty MZ content and the portable ZIP for exact published-file coverage, lengths and SHA-256 contents. Built-in ZipFile includes hidden payload files, unlike the prior Compress-Archive path. No new dependency, version or public packaging flag was introduced.

Individual File.Replace/Move operations preserve complete files. A flushed journal and hashed previous-file copies support rollback and next-invocation recovery before building. Recovery rejects corrupt metadata/backups and unsafe paths, preserving evidence; it removes newly created final outputs when an interrupted first publication had no prior artifact. Journal completion precedes cleanup, making recovery/cleanup repeatable. Two filenames cannot be replaced in one atomic filesystem operation: readers must wait for successful packaging completion. Hardware/power-loss behavior is not verified. A terminated process can retain its staging evidence; successful/handled-failure runs remove their own staging files.

Verification: real `scripts/package.ps1 -NoRestore -Fast -NonInteractive` passed, including a Release build with **0 warnings, 0 errors**, NSIS compilation, ZIP validation and promotion. `scripts/test-package-artifacts.ps1` passed with a real minimal NSIS fixture and isolated sentinel outputs: injected compiler failure, locked-payload compression failure, invalid setup, locked second-artifact promotion with rollback, termination of our own child after first replacement, repeatable recovery, corrupt JSON/backup preservation, hidden files, successful replacement and first publication/recovery. The regression is included in Check; the full gate was not rerun because it also clears retained ignored evidence. Native installer ownership tests also pass.

The first interruption test identified PowerShell converting a null backup argument into an empty string (`The path is empty. (Parameter 'path')`); File.Replace now receives `[NullString]::Value`. Failure assertions check the expected error rather than accepting arbitrary exceptions. Lint initially reported plural helper nouns; names were corrected without relaxing rules. Final lint: **30 scripts OK**. An auxiliary recursive scratch probe was rejected by automatic approval review before execution; a bounded named-file probe identified the issue safely, with no remaining probe files.

Primary sources checked on 2026-10-08: [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0), [ZipFile.CreateFromDirectory](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.createfromdirectory?view=net-10.0) and [Compress-Archive limitations](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.archive/compress-archive).

## Tracker cleanup and residuals

Removed completed AUDIT-18/20/21/23/24 entries. AUDIT-19/22 now contain only outstanding interactive validation. AUDIT-35 was narrowed to accepted-choice autosave/result handling; unused file/debug-sink packages were added to existing AUDIT-28. New AUDIT-36 covers the daily writer's missing file-size/retention limits; AUDIT-37 records remaining legacy protocol registrations. No duplicate findings were added.

The previous session handoff is historical documentation and was removed from pending work. The scratch-cleanup entry was also removed after `Test-Path -LiteralPath` returned false for all four recorded directories: `artifacts/audit-probe-20261005`, `artifacts/bin/AuditProbe`, `artifacts/obj/AuditProbe`, `tmp/release-actionlint-20261006`. No deletion of those historical directories was performed; their prior disappearance is not attributed to this session. The tracker contains **20 pending entries** after all three remediation batches. Next source-remediation task: AUDIT-25, then AUDIT-26. Live/hosted P1 validations and long-idle observation remain open.

## Final verification

| Command | Actual output |
| --- | --- |
| `scripts/run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive` | `PASS: 329/329 unit tests passed.` TRX: 329 executed/passed, 0 failed; five inactive smoke methods included. Fifteen retired parser cases removed, five registry ownership cases added. |
| `scripts/run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive` | Release build succeeded; 0 warnings, 0 errors. |
| `scripts/run.ps1 -Task Format -NoRestore -NonInteractive` | `PASS: Verifica formato completata.` |
| `scripts/run.ps1 -Task Lint -NonInteractive` | `PASS: All scripts linted (30 scripts OK).` |
| `scripts/test-installer-owned-files.ps1` | `PASS: NSIS removes owned files/protocol and preserves unrelated files and protocol owners.` |
| `scripts/test-package-artifacts.ps1` | `PASS: Package failures/rollback, process-interruption recovery, corrupt journal, hidden payload, replacement and first publication.` |
| `scripts/package.ps1 -NoRestore -Fast -NonInteractive` | `PASS: Setup NSIS and Portable ZIP generated (Release).` |

Tracker JSON/unique IDs/completed-task removal, local documentation links and `git diff --check` pass. `AGENTS.md` remains below 2500 characters. Reviewed the edited scope, including new tests/report and preserved earlier-batch changes. Isolated logging/registry/installer/package/probe fixtures were removed; no dependency versions/lockfiles changed. Commit/push was requested separately for the handoff below. Full Check, real-app interactive UI and live/hosted qualification were not run; real packaging and isolated native installer fixtures passed. No personal application store or actual URL handler was touched.

## Session handoff — 2026-10-08

The user explicitly requested saving the complete session work, committing on `main` and pushing to `origin/main`. The tracker handoff entry is included in the same save commit. Before committing, `git fetch origin` confirmed the default branch is `main` and `HEAD...origin/main` has zero commits on either side. The edited WinUI bindings, guards, localization, theming and disposable lifetimes were reviewed with the maintained winui-code-review skill; interactive validation remains explicitly open under AUDIT-19/22.

On resuming, verify the save commit through `git log`, remote equality and the working tree, then continue with AUDIT-25 and AUDIT-26. There are **20 pending tasks** plus one session-context entry. Retain ignored build/audit artifacts and personal/legacy data. The prior verification table remains the executed evidence; no full Check, live Windows Update operation, hosted release or personal-store UI mutation was performed for this handoff. Commit/push verification is reported after execution, rather than recording an unverified hash here.
