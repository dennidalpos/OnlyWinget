---
name: winui-dev-workflow
description: "Build, launch or diagnose OnlyWinget WinUI 3 using the repository PowerShell task runner and pinned toolchain."
---

# OnlyWinget build and run

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Use [verified repository commands](../onlywinget/references/commands.md). Build/typecheck does not launch the application. Restore only when dependencies/targets changed or restore errors require it; preserve lockfiles through the toolchain.

- Diagnose the actual compiler/XAML/restore error before changing versions. Do not automatically upgrade NuGet packages or generate another project.
- For launch, scripts/run.ps1 -Task Dev uses the repository executable path. The app is unpackaged and self-contained; a direct owned executable launch is supported.
- A launch uses the real LocalApplicationData store. For persistence edits, destructive log cleanup or install/update validation, use an isolated profile instead of a personal store.
- Do not close existing instances merely to build. StopRunningInstance requests graceful closure only for this checkout's generated executable paths; refused closure fails rather than forcing termination.
- The bundled BuildAndRun.ps1 is an optional upstream helper, not the OnlyWinget build contract. Its analyzer is conditional on a DLL actually being available; do not claim analysis ran because a targets file exists.
- Missing WinApp UI tooling does not block compilation/offline tests. Use the setup skill when the requested workflow actually needs an unavailable prerequisite.

Report executed commands and outputs, with interactive/live checks explicitly pending when not performed.
