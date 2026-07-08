# OnlyWinget Development Commands Reference

All workspace build, lint, format, test, launch, validation, and packaging tasks are routed through PowerShell scripts located in the [scripts/](file:///d:/GITHUB/OnlyWinget/scripts) directory. Prefer the wrapper scripts over raw `dotnet` or manual MSBuild parameters.

---

## 1. Core Workflow Commands

### Project Initialization and Restore
Run setup to restore NuGet packages with lock validation. Run this whenever dependencies, targets, or RID configurations change:
```powershell
# Standard setup
.\scripts\run.ps1 -Task Setup -NonInteractive

# Force re-evaluation of lock files
.\scripts\run.ps1 -Task Setup -ForceEvaluate -NonInteractive
```

### Code Quality and Linting
Enforce style guidelines, standard formatting, and powershell script quality rules:
```powershell
# Auto-format C# source files using dotnet format
.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive

# Run PSScriptAnalyzer on scripts
.\scripts\run.ps1 -Task Lint -NonInteractive
```

### Build and Compilation
Build without executing or packaging, or execute type checking:
```powershell
# Compile check in Release configuration
.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive

# Full build in Release configuration
.\scripts\run.ps1 -Task Build -Configuration Release -NonInteractive
```

### Automated Testing
Execute C# unit tests and integration tests:
```powershell
# Run standard offline unit tests
.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive

# Run unit tests including live Winget commands (requires winget installation)
.\scripts\run.ps1 -Task Test -Configuration Release -RunWingetSmoke -NonInteractive
```

### Complete Integration Checks
Runs format verification, script linting, compilation checks, unit tests, and packages release installers in a single run. Use this before staging files:
```powershell
.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
```

---

## 2. Packaging and Release

OnlyWinget targets the `win-x64` platform. Release packages include both a self-contained portable ZIP and a WiX-based MSI installer (bundled with the Windows App Runtime).
```powershell
.\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive
```
*Note: Staged installer assets, compiler outputs, and WiX builds are outputted inside [artifacts/](file:///d:/GITHUB/OnlyWinget/artifacts).*

---

## 3. Advanced and Environment-Dependent Verification

### Installer Lifecycle Validation
Validates the MSI install, upgrade path from previous versions, and clean uninstall on the current system (requires an elevated clean x64 Windows environment):
```powershell
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

### Installed Startup Validation
Verifies that an installed executable starts and remains responsive:
```powershell
.\scripts\run.ps1 -Task ValidateInstalledStartup -NonInteractive
```

### Graphical UI Automation Tests
Executes visual flow, page routing, element values, and list scroll checks using the `winapp` CLI and Windows UIAutomation interfaces:
```powershell
# Requires a running app instance and the winapp CLI tool installed
.\scripts\ui-test.ps1 -AppPid <PID> -NonInteractive
```
*Note: For scroll/mouse wheel assertions, the automated pointer will be repositioned directly over the UI element before verifying state changes.*
