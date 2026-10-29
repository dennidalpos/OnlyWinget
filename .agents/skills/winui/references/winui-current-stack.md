# OnlyWinget stack

Repository toolchain and package pins verified against [global.json](../../../../global.json), [Directory.Build.props](../../../../Directory.Build.props) and `.csproj` manifests. Preserve these pins and lockfiles (`packages.lock.json`) unless an explicit upgrade is requested.

- **SDK (`global.json`)**: `.NET SDK 10.0.403` with `"rollForward": "disable"`. Locked NuGet restore (`RestorePackagesWithLockFile=true`) requires this exact SDK; regenerate lockfiles via `.\scripts\fix-lockfiles.ps1` when changing dependencies or the SDK pin.
- **Build policy (`Directory.Build.props`)**: `Deterministic=true`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=10.0`, `EnableTrimAnalyzer=true`, `EnableSingleFileAnalyzer=true`, `EnableAotAnalyzer=true`. Outputs go to `artifacts/bin/`, `artifacts/obj/` and `artifacts/publish/`.
- **UI project (`src/OnlyWinget/OnlyWinget.csproj`)**: `net10.0-windows10.0.17763.0`, `TargetPlatformMinVersion=10.0.17763.0`, `RuntimeIdentifier=win-x64`, `SelfContained=true`, `WindowsAppSDKSelfContained=true`, `WindowsPackageType=None`, `UseWinUI=true`, `IsTrimmable=true`, `Version=1.0.2`.
- **Core packages**:
  - `Microsoft.WindowsAppSDK` `2.3.1`
  - `CommunityToolkit.Mvvm` `8.4.2` (`MVVMTK0045` suppressed for field-based `[ObservableProperty]` generators)
  - `Microsoft.Extensions.Hosting` `10.0.11`, `Microsoft.Extensions.Logging.Abstractions` `10.0.11`, `Microsoft.Extensions.Caching.Memory` `10.0.11`
  - `Serilog.Extensions.Hosting` `10.0.0`
  - `Microsoft.EntityFrameworkCore.Sqlite` `10.0.11`, `SQLitePCLRaw.bundle_e_sqlite3` `2.1.12`
  - Test project (`tests/OnlyWinget.Tests/OnlyWinget.Tests.csproj`): `Microsoft.NET.Test.Sdk` `18.9.0`, `xunit` `2.9.3`, `xunit.runner.visualstudio` `3.1.5`
- **Manifest & distribution**: `src/OnlyWinget/app.manifest` (`asInvoker`). Distributed as x64 NSIS multi-user setup (`src/OnlyWinget.Setup/OnlyWinget.nsi`, NSIS `3.13.0` / `v3.13` in CI) and self-contained portable ZIP; no MSIX manifest or Store packaging.
- **Scripting & automation**: PowerShell 7+ (`pwsh`) for `scripts/*.ps1` (linted with `PSScriptAnalyzer`); Windows PowerShell 5.1 (`powershell.exe`) for specific WUA COM fallback and NSIS `Sysnative` process-closure paths; `winapp` CLI for interactive UI Automation (`scripts/ui-test.ps1`).

Sources: [Windows App SDK stable channel](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/stable-channel), [SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), [deployment models](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/).
