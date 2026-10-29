---
name: winui-setup
description: "Inspect or install prerequisites explicitly needed for an OnlyWinget development workflow; preserve repository SDK/package pins."
---

# OnlyWinget prerequisites

Use with [the project skill](../onlywinget/SKILL.md) when the user requests environment setup or a required tool is missing (`agents/openai.yaml` sets `allow_implicit_invocation: false`).

- Inspect [global.json](../../../global.json), [Directory.Build.props](../../../Directory.Build.props), `.csproj` files and installed tools first (or run the read-only probe `pwsh -NoProfile -File .agents/skills/winui/scripts/check-winui-environment.ps1`).
- **Pinned toolchain**:
  - **PowerShell 7+ (`pwsh`)**: required for all `scripts/*.ps1` entrypoints. Windows PowerShell 5.1 (`powershell.exe`) is used by specific WUA fallback and NSIS `Sysnative` helpers.
  - **.NET SDK `10.0.403` (`global.json`, `rollForward: disable`)**: required by `dotnet restore --locked-mode` (`RestorePackagesWithLockFile=true`). Do not upgrade NuGet packages or SDK pins unless requested; when an SDK or dependency change is requested, regenerate lockfiles via `.\scripts\fix-lockfiles.ps1`.
  - **`PSScriptAnalyzer`**: required for `.\scripts\run.ps1 -Task Lint -NonInteractive`.
  - **NSIS 3.x (`makensis.exe`, `3.13.0` / `v3.13` in CI)**: required for `Package` and the NSIS regressions (`test-installer-owned-files.ps1`, `test-package-artifacts.ps1`, `test-process-ownership.ps1`).
  - **`winapp` CLI**: required only for interactive `.\scripts\ui-test.ps1` runs, not for compilation, offline xUnit tests or packaging. Windows Developer Mode and MSIX templates are not required for OnlyWinget (`WindowsPackageType=None`).
- `.\scripts\run.ps1 -Task Setup -NonInteractive` runs locked NuGet restore. `scripts/support/ScriptHelpers.ps1` can bootstrap missing prerequisites (`dotnet`, `PSScriptAnalyzer`, `NSIS`) unless `ONLYWINGET_SKIP_AUTO_INSTALL=1` is set.
- Install only what the requested workflow requires, then verify with the tool's actual version/help command and rerun the blocked task.

Sources: [global.json SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), [Windows App SDK setup](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment).
