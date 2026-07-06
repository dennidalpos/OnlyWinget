---
name: onlywinget
description: Use this skill when developing, testing, packaging, or modifying the OnlyWinget C#/.NET 10 WinUI 3 application, including its clean architecture layers, PowerShell run scripts, winget & Windows Update APIs, and WiX installer.
---

# OnlyWinget Development Skill

This skill acts as the primary developer guide and runtime instructions for agents working on the [OnlyWinget](file:///d:/GITHUB/OnlyWinget) project. It ensures that all modifications preserve the Clean Architecture boundaries, use the correct tools/scripts, and adhere to structural rules.

## Documentation Map

- **Architecture Details**: Deep-dive on architecture, layers, threading, and interop rules in [architecture.md](file:///d:/GITHUB/OnlyWinget/skills/onlywinget/references/architecture.md).
- **Workflow & Testing Commands**: Detailed parameters and execution flags in [commands.md](file:///d:/GITHUB/OnlyWinget/skills/onlywinget/references/commands.md).

## Critical Developer Rules

1. **Dependency Direction (Strict Onion)**:
   - `WinUI Presentation -> Application -> Domain`
   - `Infrastructure -> Application -> Domain`
   - **Do NOT** let `Application` depend on `Infrastructure` or `WinUI`.
   - **Do NOT** let `Domain` depend on *anything* else (keep it pure C#).

2. **Concurrency & Thread Safety**:
   - Guard shared in-memory updates (like `packageMetadata` additions) with `lock` sync primitives.
   - Guard file/persistence writes (`JsonWorkspaceStore`, `JsonSourcePreferenceStore`) using instance-scoped `SemaphoreSlim(1,1)`.
   - Always pass a real `CancellationToken` to every cancellable operation. Never use `CancellationToken.None` for work designed to support cancellation.

3. **WinUI Presentation & MVVM**:
   - ViewModels bound via `x:Bind` must be declared `public` (constructors can remain `internal`) for successful compilation.
   - Do NOT use `XamlReader.Load` to parse inline XAML strings at runtime. Generate grid-row cell layouts programmatically in C# (e.g., using `OnlyWingetTableRow`).
   - Use page item source collections stably and update them; avoid replacing whole collections unless necessary.
   - Publish state changes using the instance-scoped `OnlyWingetApplication.StateChanged` event. Avoid static events or manual page broadcasts.

4. **Localization**:
   - Only English and Italian strings are supported.
   - Do NOT create `.resw` or `.resx` files. All string values are stored in [TextResources.cs](file:///d:/GITHUB/OnlyWinget/src/OnlyWinget/TextResources.cs) code dictionaries.

5. **External Processes & COM**:
   - Centralize OS/winget/PowerShell capability checks in `ISystemCapabilityService`.
   - Scan Windows Updates only on explicit user action; discovery scans must not require administrative elevation.
   - Guard all process execution (`ProcessExternalProcessRunner`) and COM interop with structured failure handling. Return actionable results (`WingetOperationOutcome`, `WindowsUpdateOperationOutcome`).

## Workflow Verification

Before concluding any work, ensure you run the local script verification tasks:
- Setup: `.\scripts\run.ps1 -Task Setup -NonInteractive`
- Formatting: `.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive`
- Linting: `.\scripts\run.ps1 -Task Lint -NonInteractive`
- Build / Typecheck: `.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive`
- Tests: `.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive`
