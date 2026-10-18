---
name: winui-packaging
description: "Prepare or validate OnlyWinget self-contained NSIS setup and portable ZIP artifacts and release gates."
---

# OnlyWinget packaging and release

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

OnlyWinget supports NSIS setup EXE plus portable ZIP. MSIX tasks, certificates and Store submission are not part of this repository workflow.

Follow [release.md](../../../docs/release.md), [operations](../../../docs/operations.md) and [the NSIS skill](../nsis-installer/SKILL.md).

- Preserve the 1.0.x version policy and exact tag/project/SHA matching. Packaging alone is not authorization to create/push a tag or publish a release.
- Keep the per-worktree packaging lock. Build both artifacts in destination-volume staging; validate before individual promotion.
- Preserve recovery journals/backups when validation or restoration fails. The next package invocation recovers an interrupted promotion before building.
- Check all portable payload files, including hidden files, against the published output. Never replace a valid artifact with partially generated output.
- Release CI gates and exact assets must pass for the selected tag. AUDIT-12's hosted mismatch/full-gate failure prevention is qualified in the [2026-10-18 handoff](../../../docs/tracker-review-2026-10-18.md#continuation--audit-12-hosted-negative-qualification-completed), using unchanged production commands in a read-only branch fixture; do not merge that fixture or treat it as positive publication qualification. Isolated regressions alone do not establish hosted behavior.
- Ownership and true older-payload upgrade validation remain separate from successful artifact creation.

Read [source-generator patterns](references/sourcegen-patterns.md) only if the task actually changes the related code-generation/interop path; it is not a packaging prerequisite.

Sources: [Microsoft deployment models](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/), [NSIS manual](https://nsis.sourceforge.io/Docs/).
