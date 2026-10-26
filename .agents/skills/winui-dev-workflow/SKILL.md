---
name: winui-dev-workflow
description: "Build, launch or diagnose OnlyWinget WinUI 3 using the repository PowerShell task runner and pinned toolchain."
---

# OnlyWinget build and run

Use with [the project skill](../onlywinget/SKILL.md) and [verified repository commands](../onlywinget/references/commands.md).

- Run tasks through `.\scripts\run.ps1 -Task <Task>` from the repository root in PowerShell 7 (`pwsh`). `Build` and `Typecheck` compile into `artifacts/bin/OnlyWinget/<Configuration>/net10.0-windows10.0.17763.0/win-x64/OnlyWinget.exe` without launching the app.
- Restore (`.\scripts\run.ps1 -Task Setup -NonInteractive`) uses NuGet `--locked-mode` against `global.json` (`.NET SDK 10.0.402`, `rollForward: disable`). Diagnose compiler/XAML/restore errors before changing versions; regenerate lockfiles only via `.\scripts\fix-lockfiles.ps1`.
- Launch the unpackaged self-contained app via `.\scripts\run.ps1 -Task Dev -Configuration Release -NoRestore -NonInteractive` (or launch the built `OnlyWinget.exe` directly).
- Normal execution uses `%LOCALAPPDATA%\OnlyWinget`. For isolated persistence or log-cleanup testing, set `ONLYWINGET_DATA_ROOT` only in the launched process's environment to a fully qualified temporary directory. This isolates SQLite, legacy JSON migration, source preferences, settings and logs, but does not isolate machine-wide WinGet or Windows Update state.
- Do not close running instances merely to inspect code. `-StopRunningInstance` (`scripts/support/CloseOwnedApplication.ps1`) requests graceful `CloseMainWindow` only for executables under this checkout's `artifacts/bin`, `artifacts/obj` and `artifacts/publish`, and fails closed if closure is refused. Inaccessible/null-module candidates are ignored without closure; their unreadable paths provide no ownership or unlocked-output guarantee.
- `.agents/skills/winui-dev-workflow/BuildAndRun.ps1` and `analyzer/Microsoft.WindowsAppSDK.Analyzers.targets` (plus the git-ignored `analyzer/Microsoft.WindowsAppSDK.Analyzers.dll`) are preserved upstream helpers, not the OnlyWinget build contract: `BuildAndRun.ps1` requires Windows Developer Mode, looks under `bin\<Platform>\<Config>` rather than `artifacts/bin/`, and skips injecting its analyzer whenever root `Directory.Build.props` exists. Always use `scripts/run.ps1` for OnlyWinget builds and typechecks.
