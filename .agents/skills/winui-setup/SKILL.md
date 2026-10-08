---
name: winui-setup
description: "Inspect or install prerequisites explicitly needed for an OnlyWinget development workflow; preserve repository SDK/package pins."
---

# OnlyWinget prerequisites

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Use this skill when the user requests setup or a needed tool is missing. Inspect global.json, project files, installed SDKs, PowerShell and NSIS first; report the actual missing prerequisite.

- Repository scripts require PowerShell 7+. Legacy installer commands may invoke Windows PowerShell for a specific validation.
- Keep the pinned .NET SDK/package versions and the self-contained x64 deployment model. Do not upgrade tools, templates or dependencies just because a generic guide says latest.
- scripts/run.ps1 -Task Setup restores dependencies. Existing scripts can bootstrap missing prerequisites; ONLYWINGET_SKIP_AUTO_INSTALL=1 makes absence a failure.
- WinApp CLI is needed for the UI automation path, not every build/test/package task. Templates and Developer Mode are not prerequisites for maintaining this existing app unless the chosen tool proves otherwise.
- Install only what the requested workflow needs. Machine-wide registry settings, certificate trust or additional distribution models require that specific scope.
- Verify the new tool with its real version/help and rerun the previously blocked command. Do not declare prerequisites installed from a planned command.

Sources: [global.json SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), [Windows App SDK setup](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment).
