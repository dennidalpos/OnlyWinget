# Tracker review — 2026-10-09

The user authorized direct validation on this PC. UI fixtures used the actual Release WinUI executable and the normal data path. With no existing OnlyWinget process, the personal data directory was temporarily moved to a unique sibling on the same volume; a fixture replaced it during execution. After graceful closure, fixture data was retained under ignored evidence and the original directory restored. File counts and SHA-256 hashes verified restoration. No personal database migration or log clearing was part of these fixtures.

Starting branch: `main`; HEAD and the remote main branch resolve to `874facbb049a40f2e770c94663a83a4c426512c3`. Three preexisting generated lockfile changes are preserved. The old save handoff is removed after verifying the remote save commit. Commit and push were authorized subsequently for the session handoff on 2026-10-10.

## Completed — AUDIT-10 and AUDIT-27

Actual UI execution exposed defects in the test runner before qualification. Its advanced PowerShell wrapper bound native `-a` to the `Arguments` parameter, raising `A positional parameter cannot be found that accepts argument 'ui'`. A plain argument-forwarding wrapper now preserves native switches. Keyboard focus targets the focusable Home item instead of the NavigationView container. Picker filtering checks optional JSON properties because current winapp output omits `title` for unnamed windows. Checks are preserved; none are disabled.

The corrected runner passes all **10 real UI checks** on an empty workspace and again on a persisted preset containing `VideoLAN.VLC` from the live winget source. The latter run keeps a separate OpenFileDialog open in another process. OnlyWinget's new owned import picker closes; the unrelated picker retains its PID and HWND `1510920`. Table identities, checkbox/selection states and collapsed preset selection remain unchanged after cancellation. Keyboard focus moves, source enablement changes and restores, table scrolling is exposed, and navigation stays within the actual window at 640×720, 900×760 and 1280×800. Interactive metadata inspection passes after the owned modal picker is closed.

Evidence: `artifacts/ui-tracker-20261009/run2/corrected/results.json`, `run2/populated/results.json` and corresponding layout screenshots. The initial failed runs remain evidence. A separate attempted run with a retained invalid package draft is not qualification: the navigation guard correctly prevents route/import changes. The draft came from a real rejected `UIAudit.Package` lookup and was replaced with the valid VLC identity before the final populated run.

AUDIT-10 and AUDIT-27 are removed from pending work. Remaining edit and log-viewer scenarios are recorded separately below.

Current primary contract: [Microsoft UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32). Installed winapp 0.7.1 command help and actual JSON output were inspected before adapting the runner.

## AUDIT-19 — actual persistence failure and retry

An actual invalid package lookup retained its draft after flyout dismissal and blocked navigation. A trigger in the fixture database rejected preset insertion with `UI fixture save failure`. The accepted in-memory preset remained visible with locked fields; cancelling the navigation confirmation retained it. After removing the fixture-only trigger, Apply retried persistence and the database contained exactly two presets and the original VLC item, without duplicate mutation. Restart loaded the saved presets and active selection.

Evidence: `artifacts/ui-tracker-20261009/run2/failed-save.json`, `failed-save-navigation.json`, `after-save-cancel.json`, and the retained fixture databases in `run2/fixture-data` and `run3/fixture-data`. Source-only/same-ID-different-source edits, mutation cancellation, unchanged drafts, tracker/preset/mode navigation and system-close/reentrancy remain unqualified. AUDIT-19 stays open.

## AUDIT-22 — viewer fixes and interactive results

Opening the viewer failed with `XamlParseException`: `LayerOnCanvasFillColorDefaultBrush` does not exist in the pinned WinUI theme resources. This triggered the existing Explorer fallback and explains the folder error reported during the temporary data-path swap. Replacing it with `LayerFillColorDefaultBrush` restores the viewer. The dialog also remained dark when the host was explicitly light; the viewer, shared confirmation service and pending-edit dialog now request the root's actual theme. A real open/close regression was added to the UI runner.

Actual Light and Dark screenshots were inspected after the final build (`artifacts/ui-tracker-20261009/themes/light.png` and `dark.png`). A High Contrast screenshot shows readable level text and borders (`log-actions/high-contrast.png`). The native scratch helper then crashed PowerShell with `0xc0000374`; the app stayed alive. The helper failure does not establish an OnlyWinget defect, and the interrupted sequence is not reported as a complete pass. Native High Contrast/clipboard code was removed from the scratch runner before a managed-only follow-up.

The follow-up verified inline clear cancellation, a real locked-file failure retaining memory entries and confirmation, and successful retry after releasing the lock. `log-actions-managed/assertions.json` records assertions against the actual UI trees. Export to a real nonempty log file and native picker cancellation passed in `run3/export.log`, `export-success.json` and `export-cancelled.json`.

The shutdown probe failed its assumption that the owner could close during the modal save picker: the main window remained open for five seconds. Cancelling only the app-owned picker allowed graceful closure. This observed limitation stays under AUDIT-22; copy success/failure, export I/O failure and cancellation of native picker lifetime remain unqualified. No global picker shutdown or production cancellation workaround was introduced.

The current [WinUI theme resources](https://github.com/microsoft/microsoft-ui-xaml/blob/main/controls/dev/CommonStyles/Common_themeresources_any.xaml), the pinned package's `generic.xaml`, [ThemeResource contract](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/themeresource-markup-extension), [ContentDialog contract](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.contentdialog?view=windows-app-sdk-2.0) and [desktop picker specification](https://github.com/microsoft/WindowsAppSDK/blob/main/specs/Storage.Pickers/Microsoft.Windows.Storage.Pickers.md) were consulted. Runtime qualification applies to the locally built source, not to an older staged setup/ZIP.

## Personal data and cleanup

After the reported Explorer error, full-directory swapping was stopped. Later probes kept the log directory continuously available. Theme probes preserved original settings bytes. Clear probes protected each of the 14 personal logs by an exact individual rename in the same directory; only newly generated test logs were cleared. All original logs and settings were restored with identical SHA-256 hashes, including manual recovery after the scratch PowerShell crash. High Contrast was confirmed disabled afterward. Restoration records are retained in `run3/restoration.txt`, `themes/restoration.txt`, `log-actions/restoration.txt` and `log-actions-managed/restoration.txt`.

The final read-only check matches all **162 personal files** against the original count and SHA-256, and finds no running OnlyWinget process. Evidence: `artifacts/ui-tracker-20261009/final-restoration.txt`. The normal log directory exists.

The old `tmp/skill-validation-a74b195f6307432291944e7f020d607c` fixture is already absent. Its obsolete cleanup entry is removed; no deletion retry was performed. Automatic execution review rejected this session's guarded copy-and-removal of four scratch scripts before execution with `blocked by policy`. The four files remain under `tmp/ui-tracker-20261009`: `session.ps1`, `unrelated-picker.ps1`, `themes.ps1`, `log-actions.ps1`; a separate cleanup residual is tracked. No alternative deletion was attempted. No unrelated processes, personal stores or registry handlers were removed.

## External prerequisites and partial hosted qualification

A native read-only Windows Update search succeeded (`ResultCode=2`) with zero available software updates. AUDIT-03/04 therefore retain real download/install cancellation and exact-revision installation qualification; no update or package was installed. Evidence: `artifacts/ui-tracker-20261009/windows-update-inventory.json`.

The hosted [manual branch dispatch without a tag](https://github.com/dennidalpos/OnlyWinget/actions/runs/37995312437) failed at `Validate Release Tag and Version` with `Release requires an explicit vMAJOR.MINOR.PATCH tag; branch dispatch without a tag is unsupported.` Build/package and publication were skipped; GitHub still has zero releases. This qualifies the no-tag guard only. AUDIT-12 retains explicit-tag mismatch and failed full-gate publication prevention. Evidence: `artifacts/ui-tracker-20261009/hosted-release-no-tag.json`; [GitHub manual workflow documentation](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow).

The user confirmed that no genuine prior installer is available. AUDIT-31 stays open for obsolete-file reconciliation and true upgrade/uninstall validation; the current installer is not treated as an earlier payload. Pages returned HTTP 404 and no deployed landing URL was supplied. No tag, release or website was published. Long-idle observation and ownership review under other profiles remain pending. The current user's exact legacy protocol key is absent; this does not qualify other profiles or deleted portable copies.

## Verification and remaining work

- Release Typecheck: zero warnings and errors.
- `scripts/test.ps1 -Configuration Release -NoRestore -NoBuild -Full -NonInteractive`: **380 passed, zero failed/skipped**; four live Smoke tests excluded.
- Lint: **39 scripts OK**. Format verification passed.
- Two corrected real UI runs: **10/10 each**. After adding the viewer regression, a later run passed **10/11**; focus activation was refused on the active PC. The viewer open/close check passed. That run is not reported as an all-pass qualification.
- `git diff --check` passed. Preexisting generated lockfile changes were preserved without manual edits.

The tracker contains **ten remaining entries**, including the newly blocked session-scratch cleanup. Results and missing prerequisites are recorded above. Documentation and final restoration checks continued after midnight on 2026-10-10; runtime qualification occurred on 2026-10-09. Completion is not inferred from offline tests or from a screenshot alone.

## Session handoff — 2026-10-10

The user requested saving all work, committing and pushing on the default branch before changing session. The save includes viewer/theme fixes, the corrected UI runner and regression, documentation/tracker updates, and the three existing generated win-x64 lockfile changes. `main` was verified as the remote default branch; no other source changes occurred after the checks above. Local screenshots, fixture databases and recovery manifests remain ignored evidence under `artifacts/ui-tracker-20261009`, not part of the Git commit.

Resume from `PROJECT_STATUS.json` and this report. AUDIT-19/22 retain the remaining interactive cases; AUDIT-03/04 need available updates; AUDIT-12 needs explicit-tag/failing-gate hosted qualification. No genuine prior installer or deployed landing URL is available. Direct-PC test authorization remains valid; avoid further full-directory swaps and preserve personal data. The four blocked scratch scripts remain local, and the failed native High Contrast/clipboard helper must not be reused as a successful qualification. Verify the saved `main` commit against the remote before starting new work.
