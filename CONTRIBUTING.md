# Contributing to OnlyWinget

Thank you for your interest in OnlyWinget.

## Before you start

- Read [README.md](README.md) for the supported platforms, prerequisites and the official build and test commands.
- For a bug or an idea, open an issue first using the matching form, so the change can be discussed before work starts.
- Security problems are never reported in public issues: follow [SECURITY.md](SECURITY.md).

## Making a change

1. Keep the change focused on a single topic.
2. Run `.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive` in an isolated checkout. It clears that checkout's generated outputs. Include the results and identify any host checks still pending.
3. Use synthetic or redacted data only: no secrets, credentials, personal data, host paths or raw logs in code, tests, screenshots or issues.
4. Keep both user guides in sync when behavior or labels change, and update [CREDITS.md](CREDITS.md) when dependencies change.
5. After the maintainer approves the contribution, open a pull request and fill in the template. Maintainers follow the [repository's Git workflow](AGENTS.md#git-workflow-push-policy).

## Licensing

This project is proprietary (see [LICENSE](LICENSE)). Contributions are accepted only after prior written agreement with the maintainer; do not send code or assets that you are not entitled to license.

Participation is governed by the [Code of Conduct](CODE_OF_CONDUCT.md).
