# CI restore and diagnostics review — 2026-10-17

Starting commit: `80253167f52327551ac4383833a97ae375c3c48e`, verified against the remote default branch `main`. The application code has not changed since the 2026-10-10 handoff. Implementation and verification use the attached `codex/ci-restore-diagnostics` worktree; the original checkout's existing AGENTS.md change, untracked editor files and ignored UI evidence are preserved. The user subsequently authorized commit/push of this branch and monitoring its build-gate; hosted qualification results are recorded below when available.

## Verified failure and implementation

The current hosted [build-gate run](https://github.com/dennidalpos/OnlyWinget/actions/runs/38475823195) fails during locked restore, before compilation, tests or packaging. Fast mode suppresses restore stdout, and the success-only artifact report does not exist when restore fails. Artifact upload consequently also fails. The hosted logs do not expose the underlying NuGet error.

`dotnet restore OnlyWinget.sln --locked-mode --nologo` in a fresh worktree with SDK 10.0.402 reproduces NU1004: all five lockfiles request Microsoft.NET.ILLink.Tasks 10.0.12, whereas the selected SDK requests 10.0.13. Domain, Application and Infrastructure additionally contain win-x64 lock targets absent from the ordinary solution restore. This establishes the local causes; the suppressed historical hosted output cannot independently prove which NU1004 messages that runner emitted.

The exact SDK is now 10.0.402 with roll-forward disabled. `scripts/fix-lockfiles.ps1` regenerated the five lockfiles and verified locked restore. Only the SDK's implicit ILLink package changed version; explicit application package versions remain unchanged, and the three incompatible library RID targets are removed. See the official [SDK roll-forward contract](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#rollforward) and [NuGet locking contract](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies).

Check now retains restore stdout/stderr in `artifacts/check/restore.log`, shows the last 60 lines after a Fast/default failure and streams Full output. Every failing gate step attempts to write `artifacts/build-report.txt` with its name, original error and completed steps, while preserving the original failure if report writing fails. Both hosted workflows upload those diagnostics and test logs/TRX. A release rejected before the gate may have no diagnostics; this does not permit publication.

The real Check entrypoint is tested with isolated native commands in default, Fast and Full modes: restore exit 7 must remain nonzero, display diagnostics, retain both output streams and stop before format; a successful restore followed by an intentional format failure must retain the completed restore and identify the later failure. The regression failed against the original script because the restore log was missing, then passed after the fix.

The first full gate exposed a preexisting cleanup-fixture dependency omission: its copy of ScriptHelpers.ps1 could not import CloseOwnedApplication.ps1. The fixture now copies that production helper too. Its dry-run and actual generated-output cleanup checks pass while preserving all five application-data sentinels; dotnet cache commands remain fixture stubs. Production cleanup/ownership behavior is unchanged.

The second full gate built both real artifacts and passed installer ownership, but failed the packaging regression's message-only assertion for an intentionally locked output. File.Replace reported that the replaced file could not be removed, which is a documented [ReplaceFile error, ERROR_UNABLE_TO_REMOVE_REPLACED (1175)](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew#return-value). That locked-output assertion now unwraps IOException and accepts only native codes 32 or 1175, rather than depending on localized message text. It still requires the original two artifacts and unrelated file to survive unchanged. The complete isolated packaging regression passes after this test-only correction; production package promotion/recovery code is unchanged. The failure was not consistently reproduced by the unchanged standalone regression, so this is recorded as an intermittent assertion failure, not a proven production packaging defect.

A separate temporary File.Replace diagnostic probe, including guarded cleanup, was rejected before execution by automatic review (`blocked by policy`). It was not executed or retried, created no fixture or cleanup residue, and was not bypassed. Qualification continues through the repository's existing regression; no raw HRESULT from that rejected probe is claimed.

The skill reference's stale AUDIT-27 status is corrected using the 2026-10-10 handoff. AUDIT-10/27 remain closed, and AUDIT-19/22 retain their outstanding scenarios.

## Verification

| Command or evidence | Result |
| --- | --- |
| Fresh-worktree locked restore, before changes | Failed with the NU1004 package/RID mismatches described above. |
| `pwsh -NoProfile -File scripts/fix-lockfiles.ps1` | Passed regeneration and subsequent locked restore. |
| `pwsh -NoProfile -File scripts/test-check-diagnostics.ps1` | Failed before the production fix; passed all six failure-path combinations afterward. |
| `pwsh -NoProfile -File scripts/test-clean-preserves-data.ps1` | Passed after adding the missing fixture helper. |
| `pwsh -NoProfile -File scripts/test-package-artifacts.ps1` | Passed all packaging failure, rollback, interruption, corrupt-journal/backup and successful-promotion cases after the native-code assertion correction. |
| `pwsh -NoProfile -File scripts/run.ps1 -Task Lint -NonInteractive` | Passed, 40 scripts. |
| `actionlint 1.7.12 .github/workflows/build-gate.yml .github/workflows/release.yml` | Passed; official Windows archive checked against its published SHA-256 checksum. |
| `pwsh -NoProfile -File scripts/run.ps1 -Task Check -Configuration Release -NonInteractive` | Final run passed all 21 steps, including 380 executed offline tests with zero failed/skipped, Release compilation with zero warnings/errors, native regressions, real packaging and artifact analysis. Four live Smoke tests excluded. |
| Locked restore and SHA-256 comparison after actual packaging | Passed; all five lockfiles remain byte-for-byte unchanged. |
| Pre-commit `dotnet test --nologo` | Passed: 380 passed, zero failed, four live Smoke tests skipped (384 total). |
| Pre-commit `dotnet build -c Release --nologo` | Passed: zero warnings/errors. |

The final setup is 67,132,358 bytes and the portable ZIP is 96,910,221 bytes. The gate verifies the ZIP payload by path, size and SHA-256 and the setup executable header. Final artifact SHA-256 values are retained in `evidence/final-artifact-hashes.json` beside the worktree. The post-gate locked restore also passes, with all five lockfiles byte-for-byte unchanged from before packaging.

Both failed gates' reports, the first restore log, subsequent gate output and the final success report/restore log are retained outside the worktree's cleaned artifacts/tmp directories in the adjacent `evidence` directory. The final TRX and normal build report remain under this worktree's artifacts. The original checkout's historical artifacts and four scratch scripts retain their previous lengths/timestamps; its Git status still contains only the preexisting AGENTS.md change and untracked .continue directory. No evidence of personal-data restoration is claimed for this session because personal state was not used.

The tracker retains its `todos` schema and contains 11 unique pending entries. CI-RESTORE is locally verified but remains for hosted qualification. Historical Windows Update/no-tag notes are dated explicitly; no old completed work was reintroduced. Markdown links, tracker JSON/schema/uniqueness and the final Git whitespace diff check pass.

## Remaining qualification and completion criteria

The user-authorized branch publication pushed commit `16744362cc606b8842beeaf3964865daff1ef2c3`. Hosted [run 39064997767](https://github.com/dennidalpos/OnlyWinget/actions/runs/39064997767) passed locked restore, format, lint and cleanup preservation, then failed the ownership fixture because NSIS was absent at its expected path. Its actual image was [windows-2025-vs2026, version 20261007.264.1](https://github.com/actions/runner-images/blob/win25-vs2026/20261007.264/images/windows/Windows2025-VS2026-Readme.md), whose software inventory does not list NSIS. Both workflows now install Chocolatey `nsis` 3.13.0 and verify compiler v3.13 before Check. Local Chocolatey metadata confirms that package version, and the installed local compiler reports v3.13; the existing local installation was not changed.

The failed hosted run successfully uploaded artifact `onlywinget-build-gate` (ID 12112922363). Downloaded `build-report.txt` correctly identifies `process ownership regression`; `check/restore.log` contains the actual successful restore output. This qualifies artifact upload/retention after a later gate failure. Actual hosted restore-failure console/report retention is still distinct and pending. Evidence is retained under adjacent `evidence/hosted-39064997767` and `hosted-failure-39064997767.log`.

The diagnostics regression now also creates a minimal SDK solution in its temporary fixture, with an intentionally inconsistent win-x64 lock target. It invokes the unchanged Check entrypoint with the real dotnet executable and requires nonzero exit, NU1004 in console and retained restore output, and a failed-step report identifying restore. The main solution's lockfiles are unchanged. After verifying this expected failure, it retains `restore-failure-fixture.log`, `restore-failure-fixture-console.log` and `restore-failure-fixture-report.log` under artifacts/check so the existing hosted upload includes them. The extended regression and all-script lint pass locally. Hosted download of these files is the remaining negative qualification; a successful overall gate must still pass every normal stage.

CI-RESTORE is not fully hosted-qualified until a build-gate run on the corrected revision passes locked restore, all gate stages and artifact upload, and an actual failed restore run proves that the uploaded artifact retains stdout/stderr and the failed-step report. Local fixture results do not qualify GitHub's upload service. Branch publication and monitoring are now authorized; hosted results are still pending at this checkpoint.

AUDIT-12 remains separate: explicit-tag mismatch and failed-full-gate release prevention still need hosted qualification. Current GitHub inventory reports zero release workflow runs and zero releases; historical no-tag run 37995312437 now returns HTTP 404. Its retained local evidence is historical, not a live-accessible run.

The Windows Update inventory with zero updates dates to 2026-10-09 and was not refreshed. Available updates, a genuine earlier installer, a deployed landing URL, remaining interactive UI cases, long-idle observations and affected-profile protocol ownership remain external prerequisites or pending qualifications. No live application/package/update operation was performed here.
