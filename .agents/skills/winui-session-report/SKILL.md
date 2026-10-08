---
name: winui-session-report
description: "Analyze an explicitly requested OnlyWinget agent session and report concrete outcomes, failures and verification limits."
---

# OnlyWinget session analysis

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Identify the requested session and format before reading logs. The bundled Analyze-Session.ps1 supports Copilot/Claude formats; inspect its parameters first. Do not claim it parses Codex logs or select another conversation as an automatic fallback.

- For the current session, prefer the actual conversation, current tracker and audit/remediation evidence. Use tool outputs and executed commands, not intended actions, for pass/fail claims.
- Distinguish implementation, deterministic regressions, interactive/live/hosted validation and publication. Disabled smoke methods counted as Passed are not real smoke coverage.
- Report concrete failures, their resolution and remaining blockers; keep recommendations within the user's requested scope.
- Create the report only in the intended output directory and summarize without embedding an unredacted transcript, environment values or secrets.
- If the bundled parser would include raw transcript content, inspect and redact the report before displaying/sharing it. No external sharing is implied by report creation.
- Preserve existing logs and personal data. Do not modify memories, cleanup unrelated sessions or message another chat as part of analysis.

Record report artifacts and pending follow-up in repository documentation when the request calls for it.
