# Security Policy

## Supported versions

Only the latest commit on the `main` branch is supported.

## Reporting a vulnerability

Please do not open a public issue for security problems.

Use GitHub's private vulnerability reporting: open the **Security** tab of this repository and choose **Report a vulnerability**. Include the affected workflow (presets, search, WinGet updates, Windows Update, sources), reproduction steps and the impact you observed. Review logs for package names, source addresses and local paths before attaching them.

You can expect an acknowledgement within a few days. Confirmed issues are fixed on `main` and disclosed after a fix is available.

## Scope notes

- The app runs with `asInvoker` and never restarts itself elevated; source changes and Windows Update installation require an administrator-launched app.
- Package operations are delegated to the `winget` CLI and the Windows Update Agent; package sources decide what is installed, so enable only trusted sources.
- Dependencies are monitored with Dependabot and secret scanning with push protection.
