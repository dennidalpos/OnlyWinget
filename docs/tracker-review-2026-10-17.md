# CI restore and diagnostics review — 2026-10-17

Starting commit: `80253167f52327551ac4383833a97ae375c3c48e`, verified against the remote default branch `main`. The application code has not changed since the 2026-10-10 handoff. Implementation and verification use the attached `codex/ci-restore-diagnostics` worktree; the original checkout's existing AGENTS.md change, untracked editor files and ignored UI evidence are preserved. The user subsequently authorized commit/push of this branch and monitoring its build-gate; hosted qualification results are recorded below.

## Verified failure and implementation

The original hosted [build-gate run](https://github.com/dennidalpos/OnlyWinget/actions/runs/38475823195) failed during locked restore, before compilation, tests or packaging. Fast mode suppressed restore stdout, and the success-only artifact report did not exist when restore failed. Artifact upload consequently also failed. Those historical hosted logs did not expose the underlying NuGet error.

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
| `pwsh -NoProfile -File scripts/test-check-diagnostics.ps1` | Failed before the production fix; six command-failure combinations and the subsequent real NuGet NU1004 fixture pass. |
| `pwsh -NoProfile -File scripts/test-clean-preserves-data.ps1` | Passed after adding the missing fixture helper. |
| `pwsh -NoProfile -File scripts/test-package-artifacts.ps1` | Passed all packaging failure, rollback, interruption, corrupt-journal/backup and successful-promotion cases after the native-code assertion correction. |
| `pwsh -NoProfile -File scripts/run.ps1 -Task Lint -NonInteractive` | Passed, 40 scripts. |
| `actionlint 1.7.12 .github/workflows/build-gate.yml .github/workflows/release.yml` | Passed; official Windows archive checked against its published SHA-256 checksum. |
| Local `pwsh -NoProfile -File scripts/run.ps1 -Task Check -Configuration Release -NonInteractive` | Passed all 21 steps before the real NuGet fixture extension: 380 offline tests with zero failed/skipped, Release compilation with zero warnings/errors, native regressions, real packaging and artifact analysis. Four live Smoke tests excluded. |
| Hosted Check on `2b8735be2b1f742a6af7b2ae493d1e016082a30f` | Passed all 21 steps including the real NuGet negative fixture, 380 offline tests, zero-warning/error builds, packaging and artifact upload. |
| Locked restore and SHA-256 comparison after actual packaging | Passed; all five lockfiles remain byte-for-byte unchanged. |
| Pre-commit `dotnet test --nologo` | Passed: 380 passed, zero failed, four live Smoke tests skipped (384 total). |
| Pre-commit `dotnet build -c Release --nologo` | Passed: zero warnings/errors. |

The initial local gate's setup is 67,132,358 bytes and its portable ZIP is 96,910,221 bytes. The gate verifies the ZIP payload by path, size and SHA-256 and the setup executable header. Those local artifact SHA-256 values are retained in `evidence/final-artifact-hashes.json` beside the worktree; hosted artifacts are listed separately below. The post-gate locked restore also passes, with all five lockfiles byte-for-byte unchanged from before packaging.

Both failed gates' reports, the first restore log, subsequent gate output and the final success report/restore log are retained outside the worktree's cleaned artifacts/tmp directories in the adjacent `evidence` directory. The final TRX and normal build report remain under this worktree's artifacts. The original checkout's historical artifacts and four scratch scripts retain their previous lengths/timestamps; its Git status still contains only the preexisting AGENTS.md change and untracked .continue directory. No evidence of personal-data restoration is claimed for this session because personal state was not used.

The tracker retains its `todos` schema and contains ten unique pending entries after closing CI-RESTORE with the hosted evidence below. Historical Windows Update/no-tag notes are dated explicitly; no old completed work was reintroduced. Markdown links, tracker JSON/schema/uniqueness and the Git whitespace diff check pass.

## Hosted qualification — CI-RESTORE completed

The user-authorized branch publication pushed commit `16744362cc606b8842beeaf3964865daff1ef2c3`. Hosted [run 39064997767](https://github.com/dennidalpos/OnlyWinget/actions/runs/39064997767) passed locked restore, format, lint and cleanup preservation, then failed the ownership fixture because NSIS was absent at its expected path. Its actual image was [windows-2025-vs2026, version 20261007.264.1](https://github.com/actions/runner-images/blob/win25-vs2026/20261007.264/images/windows/Windows2025-VS2026-Readme.md), whose software inventory does not list NSIS. Both workflows now install Chocolatey `nsis` 3.13.0 and verify compiler v3.13 before Check. Local Chocolatey metadata confirms that package version, and the installed local compiler reports v3.13; the existing local installation was not changed.

The failed hosted run successfully uploaded artifact `onlywinget-build-gate` (ID 12112922363). Downloaded `build-report.txt` correctly identifies `process ownership regression`; `check/restore.log` contains the actual successful restore output. This qualifies artifact upload/retention after a later gate failure. Evidence is retained under adjacent `evidence/hosted-39064997767` and `hosted-failure-39064997767.log`.

The diagnostics regression also creates a minimal SDK solution in its temporary fixture, with an intentionally inconsistent win-x64 lock target. It invokes the unchanged Check entrypoint with the real dotnet executable and requires nonzero exit, NU1004 in console and retained restore output, and a failed-step report identifying restore. The main solution's lockfiles are unchanged. After verifying this expected failure, it retains `restore-failure-fixture.log`, `restore-failure-fixture-console.log` and `restore-failure-fixture-report.log` under artifacts/check so the existing hosted upload includes them. The extended regression and all-script lint pass locally.

Commit `61fead27585379adf797f62dbac1ea3410114b51` passed the full hosted gate in [run 39065395849](https://github.com/dennidalpos/OnlyWinget/actions/runs/39065395849), including NSIS provisioning and artifact upload. Commit `2b8735be2b1f742a6af7b2ae493d1e016082a30f` then passed the full hosted gate with the real NuGet negative fixture in [run 39065831955](https://github.com/dennidalpos/OnlyWinget/actions/runs/39065831955): all 21 stages, 380 executed/passed offline tests, zero build warnings/errors, real setup/ZIP generation and successful upload.

Downloaded artifact `onlywinget-build-gate`, ID 12113183478 (163,569,259 bytes), contains the normal successful restore log, final build report, test logs/TRX and all three real restore-failure fixture files. Independent readback confirms 380 executed/passed tests and zero failed/not-executed, NU1004 in the fixture log and console, and `Result: FAILED` / `FailedStep: restore` in the fixture report. The intentional failure belongs to the isolated negative fixture; the main restore and overall job pass. This qualifies the required real restore-failure retention/upload path without publishing a broken solution lockfile.

Both downloaded package sizes match the hosted build report. The setup has an MZ header; the ZIP has 590 entries including nonempty OnlyWinget.exe, OnlyWinget.dll, OnlyWinget.pri, App.xbf and MainWindow.xbf. The gate's payload-to-publish comparison passed on the runner; readback here verifies these downloaded resources rather than repeating an installed-app launch.

| Hosted payload | Bytes | SHA-256 |
| --- | ---: | --- |
| OnlyWinget-1.0.2-setup.exe | 67,130,578 | A3FD8EC06703423BB73D4F23DDAF96FB12456B2FB0AB0910C7DAE9FF02406E8E |
| OnlyWinget-1.0.2-portable-x64.zip | 96,909,778 | D45627A871D8EFE3DCBFA6B272E22BAE33612EB47D14B87498A6DDB6FEC85B00 |

The downloaded evidence and readback manifest are retained beside the worktree in `evidence/hosted-39065831955` and `evidence/hosted-proof-39065831955.json`. CI-RESTORE meets its completion criteria and is removed from the pending-only tracker. Commits were pushed only to codex/ci-restore-diagnostics; the original main checkout remains at 8025316 with its preexisting local changes.

## Remaining qualification

AUDIT-12 remains separate: explicit-tag mismatch and failed-full-gate release prevention still need hosted qualification. Current GitHub inventory reports zero release workflow runs and zero releases; historical no-tag run 37995312437 now returns HTTP 404. Its retained local evidence is historical, not a live-accessible run.

The Windows Update inventory with zero updates dates to 2026-10-09 and was not refreshed. Available updates, a genuine earlier installer, a deployed landing URL, remaining interactive UI cases, long-idle observations and affected-profile protocol ownership remain external prerequisites or pending qualifications. No live application/package/update operation was performed here.
