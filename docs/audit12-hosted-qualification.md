# AUDIT-12 hosted negative qualification fixture

This branch is a disposable qualification fixture, not a production workflow change. Do not merge its `release.yml` into `main`. It starts from `52ddde918ffb92e5afb675700198e00b399d2a1e`; the primary checkout's uncommitted app-data isolation, UI evidence, tracker, AGENTS.md and editor configuration are not included.

## Scope and safeguards

The existing `.github/workflows/release.yml` path is already registered for manual dispatch on the default branch. On this fixture branch it accepts only `qualification_case`, removes the tag-push trigger, checks out the selected fixture branch's exact dispatch SHA and creates an annotated tag only inside that runner's checkout. The mismatch case uses `v1.0.999997`; the gate-failure case uses the checked-out project's exact version. No remote tag is created or pushed.

The workflow's GITHUB_TOKEN has `contents: read` and checkout does not persist credentials. No additional token, secret, environment, publication override or production permission is added. GitHub's [token permissions](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token) and [release API contract](https://docs.github.com/en/rest/releases/releases#create-a-release) establish that this token cannot create a release, even if an unexpected control-flow regression reaches the publisher. The action's actual run state must nevertheless be `skipped`; a denied publication attempt is a failed qualification.

The production validator, full Check command, final SHA/assets validation and publisher step/default success condition remain unchanged. For `gate-failure` only, a runner step adds the incompatible `net10.0/win-x64` target to the Domain lockfile immediately before the real full Check entrypoint. This is an intentional, runner-local NU1004 failure; no broken lockfile is committed. Source app code, package versions and production scripts remain at the base commit.

`scripts/test-hosted-release-fixture.ps1` extracts and executes the two fixture setup blocks in isolated local clones, compares production commands/publisher against the base workflow, checks step order/read-only/manual-only configuration and runs the real production validator and Check entrypoint. It retains its clones, identity/injection records, command logs, restore diagnostics and reports in a new supplied evidence directory. Those local tests cannot establish GitHub step skipping.

## Local checks

Evidence is retained outside the checkout under `../evidence/local-negative-cases` and `../evidence/local-negative-cases-final`.

| Check | Result |
| --- | --- |
| `dotnet restore OnlyWinget.sln --locked-mode --nologo` | Passed on the clean base checkout. |
| `scripts/test-hosted-release-fixture.ps1 -OutputDirectory <new evidence directory>` | Explicit existing-tag/version mismatch rejected; valid tag accepted, then unchanged Check rejected the real injected NU1004 at restore. Production commands/publisher and guard ordering matched. Hosted result remains unqualified. |
| `scripts/test-release-validation.ps1` | Passed refs/commits/versions/assets/tag drift, including lightweight and annotated tags. |
| `actionlint 1.7.12 .github/workflows/release.yml .github/workflows/build-gate.yml` | Passed. |
| `dotnet test --nologo` | 380 passed, zero failed, four live Smoke tests skipped (384 total). These are the base commit's tests, not the primary checkout's separate 385-test app-data change. |
| `dotnet build -c Release --nologo` | Passed, zero warnings/errors. |
| `scripts/run.ps1 -Task Lint -NonInteractive` | Passed, 41 scripts. Initial run found assignment to PowerShell's automatic Matches variable; renamed to stepMatches before the passing run. |

## Hosted execution after explicit commit/push authorization

The intended destination is `origin/codex/audit12-hosted-qualification` in public repository `dennidalpos/OnlyWinget`. Commit only this fixture workflow, its local regression and this guide; leave the primary checkout untouched. Its normal build-gate will run after push. Do not create a PR or merge the fixture, and do not push any tag or create a release.

Run the existing workflow by file path and explicit branch, as described by [GitHub's manual-dispatch contract](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow):

```powershell
gh workflow run release.yml --repo dennidalpos/OnlyWinget --ref codex/audit12-hosted-qualification -f qualification_case=tag-mismatch
gh workflow run release.yml --repo dennidalpos/OnlyWinget --ref codex/audit12-hosted-qualification -f qualification_case=gate-failure
```

Record each returned/discovered run ID against the exact published SHA. Read jobs/steps, logs and the `onlywinget-release-verification` artifact; do not treat an overall failed run as sufficient evidence. Expected failures must be attributable to the intended guards:

| Case | Completion criteria |
| --- | --- |
| tag-mismatch | Runner-only tag preparation succeeds; `Validate Release Tag and Version` fails with the actual tag/version mismatch. SDK setup, NSIS, full Check, final asset validation and `Create GitHub Release` are skipped. Identity evidence is uploaded. |
| gate-failure | Initial real tag/version validation and prerequisites succeed. Real `Verify and Package Release Commit` fails with NU1004, retained restore output and `FailedStep: restore`. Final asset validation and `Create GitHub Release` are skipped. Identity, injection and failure diagnostics are uploaded. |
| Both cases | Exact fixture SHA, token read permission, no remote tags/releases added, no publisher attempt. Recheck the repository's tags/releases inventory. Record retained artifact provenance and limitations. |

No positive publication, release assets, real installer upgrade or live package/Windows Update behavior is qualified by these negative cases. AUDIT-12 remains open until the hosted completion criteria above are actually verified. If branch/dispatch authorization is not granted, preserve the local fixture and report the pending hosted step rather than closing the tracker item.
