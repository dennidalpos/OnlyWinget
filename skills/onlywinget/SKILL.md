---
name: onlywinget
description: Official developer skill for OnlyWinget C#/.NET 10 WinUI 3 desktop application development, Clean Architecture layers, PowerShell script workflows, WinGet & Windows Update native COM APIs, and NSIS installer packaging.
---

# OnlyWinget Application Developer Skill

This skill provides guidelines and specifications for developing, modifying, testing, and packaging **OnlyWinget**—a modern C#/.NET 10 WinUI 3 desktop application for managing Windows package workflows.

## 1. Architectural Architecture & Layering Rules

OnlyWinget enforces strict **Clean Architecture (Onion)** dependency boundaries:

```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```

- **Domain (`OnlyWinget.Domain`)**: Pure C# domain entities, package identities, preset models, and operation planning primitives. Depends on **nothing** else.
- **Application (`OnlyWinget.Application`)**: Use-case orchestrators, workspace contracts, capability services, search/update ports, and preset import/export (`onlywinget.preset.v1`).
- **Infrastructure (`OnlyWinget.Infrastructure`)**: Relational SQLite storage via EF Core 10 (`onlywinget.db`), WinGet native COM (`Microsoft.Management.Deployment`) with `IMemoryCache` TTL caching, Windows Update native COM (`WUApiLib`), DPAPI encrypted secret storage, and process execution runners.
- **Presentation (`OnlyWinget`)**: WinUI 3 desktop shell built on `Microsoft.Extensions.Hosting` (`Host.CreateDefaultBuilder()`), Serilog structured logging, and `CommunityToolkit.Mvvm` ViewModels.

## 2. Process Execution & Security Rules

- **Process Isolation**: All external process execution must use `ProcessExternalProcessRunner`.
- **Command Argument Sanitization**: When running elevated tasks (`runas` UAC), arguments passed to `cmd.exe /c` must be sanitized via `EscapeCmdArgument` to prevent command injection vulnerabilities.
- **CancellationToken Propagation**: Every asynchronous operation must accept and honor real `CancellationToken` instances.

## 3. Native COM Interop & Resource Safety

- **Deterministic COM Cleanup**: All native COM objects created via `Activator.CreateInstance` or dynamic invocation (`Microsoft.Management.Deployment` / `Microsoft.Update.Session`) MUST be wrapped in `try / finally` blocks and released using `Marshal.ReleaseComObject`.
- **CLI Fallback**: Always provide a graceful fallback to CLI execution (`ProcessWingetCommandRunner` / `PowerShellWindowsUpdateService`) if COM registration is missing or unprivileged.

## 4. Verification Workflow

Before reporting completion on any task, run the local PowerShell workflow scripts sequentially:
- Setup: `.\scripts\run.ps1 -Task Setup -NonInteractive`
- Format: `.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive`
- Lint: `.\scripts\run.ps1 -Task Lint -NonInteractive`
- Typecheck: `.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive`
- Test: `.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive`
