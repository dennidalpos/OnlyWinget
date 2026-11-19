# Security Policy

## Supported versions

The latest commit on `main` is the supported source baseline. Include the exact commit or the version and format of the binary you tested so the maintainer can reproduce the issue.

## Reporting a vulnerability

Please do not open a public issue for security problems.

Use [Report a vulnerability](https://github.com/dennidalpos/OnlyWinget/security/advisories/new) to send a private GitHub report. Include the affected workflow, Windows build, app version or commit, prerequisites, steps and observed impact. Use synthetic input and redact tokens, private source addresses, package inventories and local paths from any diagnostic excerpt.

You can expect an acknowledgement within a few days. Confirmed issues are fixed on `main` and disclosed after a fix is available.

## Scope notes

- The app runs with `asInvoker` and never restarts itself elevated; source changes and Windows Update installation require an administrator-launched app.
- Package operations are delegated to the `winget` CLI and the Windows Update Agent; package sources decide what is installed, so enable only trusted sources.
- GitHub Dependabot security updates, secret scanning with push protection, and private vulnerability reporting are enabled. The repository runs build verification locally while GitHub Actions is disabled.
