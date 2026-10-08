# OnlyWinget stack

Verified against checkout project files on 2026-10-08. These are repository pins, not recommendations to install the globally newest versions.

- global.json: .NET SDK 10.0.301 with latestFeature roll-forward. Resolve the effective installed SDK with dotnet --version.
- OnlyWinget.csproj: net10.0-windows10.0.17763.0, minimum build 17763, win-x64 only.
- Microsoft.WindowsAppSDK 2.3.1; CommunityToolkit.Mvvm 8.4.2; Microsoft.Extensions.Hosting 10.0.11.
- SelfContained=true, WindowsAppSDKSelfContained=true, WindowsPackageType=None.
- app.manifest: asInvoker. Distribution: NSIS multi-user EXE and portable ZIP; no MSIX manifest/task.
- PowerShell 7+ repository scripts; Windows PowerShell is used by specific installer/native fallback paths. NSIS compiles setup artifacts.
- WinApp CLI supports UI automation. Templates and the optional upstream analyzer are not necessary for ordinary repo compile/offline tests.

Reinspect [the project](../../../../src/OnlyWinget/OnlyWinget.csproj), [global.json](../../../../global.json), [AGENTS.md](../../../../AGENTS.md) and [commands](../../onlywinget/references/commands.md) before relying on version/verification snapshots.

Sources for a requested version/deployment change: [Windows App SDK stable channel](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/stable-channel), [SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), [deployment models](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/).
