---
name: winui-session-report
description: "Analyze an explicitly requested OnlyWinget agent session and report concrete outcomes, failures and verification limits."
---

# OnlyWinget session analysis

Use with [the project skill](../onlywinget/SKILL.md) only when the user explicitly requests a session report (`agents/openai.yaml` sets `allow_implicit_invocation: false`).

- Identify the target session and transcript format first:
  - The bundled `.agents/skills/winui-session-report/Analyze-Session.ps1` (`-SessionId`, `-EventsFile`, `-OutputFile`, `-Format Copilot|ClaudeCode`, `-SkipSubagents`) parses only GitHub Copilot CLI (`~/.copilot/session-state/<id>/events.jsonl`) and Claude Code (`~/.claude/projects/<encoded-cwd>/<id>.jsonl`) transcripts.
  - Do not claim `Analyze-Session.ps1` parses Codex or Antigravity transcripts, and never fall back to an unrelated session ID automatically.
- For the active session or repository handoffs, inspect actual executed commands, tool outputs, 02-areas/products/OnlyWinget/tracker.json (nel dossier SecondBrain) (SecondBrain: `02-areas/products/OnlyWinget/tracker.json`) and `docs/` evidence rather than planned commands.
- Distinguish static/offline verification (e.g., 392 offline xUnit tests vs 4 skipped `[LiveFact]` smoke tests, `Typecheck`, `Lint`, isolated PowerShell fixtures) from interactive WinUI automation, live host WinGet/Windows Update mutations, installer lifecycle runs and hosted GitHub Actions runs.
- Summarize concrete changes, failures, resolutions and remaining blockers within the requested scope. Redact secrets, tokens or raw personal environment dumps before writing or displaying any report output.
