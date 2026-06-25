# Scripts

Run from the repository root with PowerShell.

| Script | Purpose |
| --- | --- |
| `setup.ps1` | Restore NuGet packages in locked mode. |
| `format.ps1` | Verify formatting, or apply with `-Fix`. |
| `lint.ps1` | Run PowerShell script lint when PSScriptAnalyzer is available. |
| `typecheck.ps1` | Build C# with warnings as errors. |
| `test.ps1` | Run xUnit tests; `-RunWingetSmoke` enables live smoke tests. |
| `build.ps1` | Build the WPF app. |
| `dev.ps1` | Launch the built app, optionally with `-Build`. |
| `package.ps1` | Build x86/x64 MSI payloads and the unified setup EXE. |
| `check.ps1` | Run the full local/CI gate. |
| `clean.ps1` | Remove generated outputs with guarded deletes. |
| `validate-installer-lifecycle.ps1` | Elevated clean-host install, upgrade, repair, uninstall validation. |

Support files live under `scripts/support/` and are not standalone entrypoints.
