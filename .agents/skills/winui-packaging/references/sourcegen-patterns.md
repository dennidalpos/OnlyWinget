# Source generation and interop review

Read this reference only when the requested change affects generated code, analyzers or native interop. `Directory.Build.props` and `.csproj` files enable `IsTrimmable=true`, `EnableTrimAnalyzer=true`, `EnableSingleFileAnalyzer=true` and `EnableAotAnalyzer=true`, while packaging uses self-contained `win-x64` without AOT/trimming publish.

- Resolve settings from `Directory.Build.props` and `src/OnlyWinget/OnlyWinget.csproj`. Preserve existing field-based `[ObservableProperty]` fields (`MVVMTK0045` is intentionally suppressed in `OnlyWinget.csproj`) and typed `UiCommand` patterns; do not migrate working ViewModels to partial properties merely to match generic samples.
- Keep existing JSON contracts, serializer options and persisted schemas (`onlywinget.preset.v1`, `source-preferences-v1.json`, `settings.json`, legacy `workspace-v1.json` reader). Any source-generated `JsonSerializerContext` must preserve wire compatibility and be verified through unit tests.
- Preserve WinUI `x:Bind` modes and generated property names. XAML compilation does not verify interactive `UpdateSourceTrigger=PropertyChanged` editor behavior.
- Native Windows Update (`ComWindowsUpdateService` / `WindowsUpdateComJob` and `PowerShellWindowsUpdateService` / `PowerShellWindowsUpdateJob`) uses dynamic COM dispatch (`HResults`, `BeginSearch`/`BeginDownload`/`BeginInstall`, `RequestAbort`, `CleanUp`); keep reflection/dynamic interop compatible with that runtime path.
- Reuse existing P/Invoke and WinRT interop in `src/OnlyWinget/` and `src/OnlyWinget.Infrastructure/`. Do not add `CsWin32` or extra NuGet packages without explicit scope approval and lockfile regeneration (`.\scripts\fix-lockfiles.ps1`).
- Never hand-edit generated files (such as `src/OnlyWinget.Setup/InstalledFiles.nsh` or `obj/` outputs); update the generator or source and rebuild through `scripts/run.ps1`.

Sources: [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation), [ObservableProperty generator](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty).
