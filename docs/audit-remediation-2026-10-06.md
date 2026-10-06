# Audit remediation

Date: 2026-10-06. Original findings: [audit-2026-10-05.md](audit-2026-10-05.md).
The [tracker](../PROJECT_STATUS.json) contains only pending work; completed findings remain documented here and in the original audit.

## AUDIT-09 — Closed

Removed application-data deletion from `Clean -All`. Cleanup preserves the database, legacy workspace, settings, source preferences and logs. No separate data-reset command was introduced.

Project discovery is materialized as an array so strict-mode validation also works with zero/one project; the isolated fixture uses one project.

Verification: the isolated `scripts/test-clean-preserves-data.ps1` fixture reproduced database deletion before the fix, then passed after the fix. It verifies dry-run preservation, actual generated-output removal and unchanged application-data sentinels, using a fixture-only dotnet stub to avoid real cache clearing. The fixture cleans up its own files. Added it to `check.ps1`.

`run.ps1 -Task Lint -NonInteractive`: **25 scripts OK**. Operational documentation and the script inventory were updated.

## AUDIT-10 — Implemented; interactive validation pending

Removed global PickerHost termination and global Escape/WM_CLOSE dispatch. Import validation inventories pre-existing windows, then closes only a new picker whose native owner chain reaches the verified test main window. Unknown ownership fails without acting. The helper rechecks ownership immediately before posting WM_CLOSE and detects owner-chain cycles.

`scripts/test-ui-dialog-ownership.ps1`: **PASS**, using real native windows with an owned nested window and an unrelated window in the same process. It checks wrong-PID rejection and main-window protection, pumps the close message and verifies the unrelated window survives. Added to the full gate. `run.ps1 -Task Lint -NonInteractive`: **27 scripts OK**.

The complete interactive WinUI import flow with a brokered file picker and an unrelated real picker has not been executed. AUDIT-10 remains in the tracker for this validation. Other UI assertion weaknesses remain under AUDIT-27.

Primary source checked on 2026-10-06: [Microsoft GetWindow ownership contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindow).

## AUDIT-11 — Closed

Removed automatic global source reset and retry from the shared command runner. Certificate failures preserve the native failure and output, append a diagnostic directing the user to inspect sources/endpoint certificates, and report failed progress. Detection supports the certificate HRESULT and case-insensitive output/error text. The explicit Sources reset command remains a separate user action; its privilege/choice/cancellation issues are already tracked under AUDIT-14/15.

Five new runner regression cases failed against the original implementation, then passed after the fix. They cover search/list/upgrade/install/source calls and verify only the original process invocation occurs. A sixth regression verifies successful output never triggers a certificate diagnostic. The offline suite changed from **265/265** baseline to **271/271 passed**, including the same five inactive smoke methods; no live source mutation was performed.

Primary sources checked on 2026-10-06: [Microsoft source command](https://learn.microsoft.com/en-us/windows/package-manager/winget/source) and [Microsoft WinGet HRESULT definitions](https://github.com/microsoft/winget-cli/blob/master/src/AppInstallerSharedLib/Public/AppInstallerErrors.h).

### Residual: AUDIT-32 — Incorrect WinGet HRESULT mappings

The adjacent classifier maps `0x8A150002` to HashMismatch and `0x8A150015` to Cancelled. Microsoft's current definitions instead identify INVALID_CL_ARGUMENTS and NO_SOURCES_DEFINED; INSTALLER_HASH_MISMATCH is `0x8A150011`, while SOURCE_DATA_MISSING is `0x8A15000F`. The existing regressions repeat the incorrect mappings, so their passing result does not validate the native contract. `0x8A15005E` correctly remains in the broad SourceUnavailable category, with its misleading comment corrected to PINNED_CERTIFICATE_MISMATCH.

Track the remaining mappings as P2 AUDIT-32: review all constants against official definitions, correct classification/retry behavior, and update regressions using native HRESULTs and localized messages. Do not treat invalid CLI arguments as a hash failure or missing source configuration as user cancellation.

## AUDIT-12 — Implemented; hosted validation pending

Release checkout resolves the explicit tag ref with history/tags available. `validate-release.ps1` checks tag format/existence, HEAD/tag SHA and Version/InformationalVersion/AssemblyVersion/FileVersion consistency. The release job runs the full `Check` gate on that SHA, then rechecks local/remote tag identity and exact-version nonempty setup/portable assets. Publication uses the validated tag/SHA and fails on unmatched required asset patterns. Removed the unconditional incomplete MSIX step and obsolete release MSI/MSIX asset patterns; the remaining unused packaging surfaces were retired under AUDIT-13.

`scripts/test-release-validation.ps1`: **PASS** for valid lightweight/annotated tags and rejection of branch/tag checkout mismatch, wrong version, expected-SHA mismatch, missing/empty assets and a different remote annotated-tag commit. The regression uses local clones/sentinel assets, creates no commits and deletes its own fixtures. Added to `check.ps1`; standalone build-gate checkout now fetches two commits for the mismatch fixture.

`actionlint 1.7.12 .github/workflows/release.yml .github/workflows/build-gate.yml`: **exit 0**, no diagnostics. The official release binary was SHA256-checked against its published checksum. `run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive`: **0 warnings, 0 errors**. No hosted workflow dispatch/publication was performed; AUDIT-12 remains for a hosted branch/tag mismatch and failed-gate publication check.

Primary sources checked on 2026-10-06: [actions/checkout](https://github.com/actions/checkout), [GitHub workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), [release action inputs](https://github.com/softprops/action-gh-release#inputs), and [actionlint releases](https://github.com/rhysd/actionlint/releases/tag/v1.7.12).

Corrected previously inaccurate atomic-packaging claims in maintained operations documentation; the implementation task remains AUDIT-24.

## AUDIT-13 — Closed

User confirmed NSIS setup plus portable ZIP distribution on 2026-10-06. Removed the exposed `PackageMsix` task and dispatch branch, unused packaging script and incomplete package manifest. Release/build-gate no longer reference obsolete MSI/MSIX output patterns; build-gate uploads the supported portable ZIP alongside setup and verification evidence. No signing secret/certificate is consumed by release automation. Generic WinUI skill references to MSIX remain applicable to those general tools; none is a live project packaging consumer.

`scripts/package.ps1 -NoRestore -Fast -NonInteractive`: **PASS: Setup NSIS and Portable ZIP generated (Release)** after a build with **0 warnings, 0 errors**. `run.ps1 -Task Lint -NonInteractive`: **28 scripts OK**. `actionlint 1.7.12` validates both changed workflows without diagnostics. Static caller/reference review found no active project consumer of the retired script/manifest/task; references in the original audit describe historical evidence. No install, uninstall, certificate import or publication was run.

Primary source checked on 2026-10-06: [Microsoft unpackaged WinUI deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app), consistent with the project's `WindowsPackageType=None` and self-contained settings.

Final artifact inspection: setup **67,122,531 bytes**, portable ZIP **96,929,091 bytes**. The ZIP opens successfully and contains `OnlyWinget.exe`, `App.xbf`, `MainWindow.xbf` and `OnlyWinget.pri`. Local documentation links, pending-only tracker consistency and `git diff --check` pass. Regression fixtures were removed, while the previously retained AuditProbe directories were preserved. The full `Check` wrapper was not rerun because it deletes all retained artifacts/tmp evidence; its affected verification stages were executed individually. Live UI, hosted release, installer lifecycle and Windows Update install/cancellation remain unverified.

Temporary linter cleanup remains blocked: automatic approval review rejected both a guarded recursive removal and a bounded nonrecursive removal of `tmp/release-actionlint-20261006` with **blocked by policy**. No further deletion attempts were made. The ignored directory contains only the official linter download and its documentation, with no production data; added it to the existing scratch-cleanup tracker entry.
