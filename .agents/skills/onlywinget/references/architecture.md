# OnlyWinget Repository Architecture

OnlyWinget is structured using **Clean Architecture** (also known as Onion Architecture) which enforces a strict one-way dependency direction.

```
WinUI Presentation ──> Application ──> Domain
Infrastructure      ──> Application ──> Domain
```

---

## 1. Architectural Layers

### Domain Layer (`OnlyWinget.Domain`)
- **Location**: `src/OnlyWinget.Domain`
- **Role**: Contains core package, preset, operation, and selection rules.
- **Dependencies**: **None**. It is pure, platform-agnostic C#.
- **Key Types**:
  - `PackageIdentity` (sealed record): Unique key representing a winget package. Binds `Id` (case-insensitive) and an optional `Source`.
  - `PackageSelection` (sealed record): Pairs a `PackageIdentity` with a `PackageAction` (`Install`, `Uninstall`, `Upgrade`).
  - `SelectionState<TKey>` (class): Generic batch-selection helper managing available/selected items and tri-state checkbox state headers (`Checked`, `Unchecked`, `Mixed`).
  - `Preset` (sealed record): A saved list of packages (`PackageIdentity`) identified by a unique name.
  - `OperationPlan` (sealed record): The planned execution queue containing lists of `PackageSelection`s.

### Application Layer (`OnlyWinget.Application`)
- **Location**: `src/OnlyWinget.Application`
- **Role**: Coordinates use cases, manages application state, defines ports (interfaces), and maps model data for presentation.
- **Dependencies**: Depends **only** on the Domain layer.
- **Key Types**:
  - `OnlyWingetApplication`: The central workflow orchestrator maintaining active state (workspace, presets, sources, updates, selections, capabilities, busy state).
  - `OnlyWingetState` (sealed record): An immutable snapshot of the entire application state.
  - **Ports (Interfaces)**: Defines how the application interacts with external services (e.g., `ISystemCapabilityService`, `IWingetCommandRunner`, `IWindowsUpdateService`, `IWorkspaceStore`, `ISourcePreferenceStore`).
  - **Presentation Mapping**: Maps `OnlyWingetState` to view-model presentation records (e.g., `DashboardPresentationState`, `UpdatesPresentationState`) via the `PresentationStateMapper`.
  - `UiCommand`: Metadata describing standard UI action commands, including labels, icon keys, and enabled states.

### Infrastructure Layer (`OnlyWinget.Infrastructure`)
- **Location**: `src/OnlyWinget.Infrastructure`
- **Role**: Implements the ports defined in the Application layer using concrete libraries and OS APIs.
- **Dependencies**: Depends on the Application and Domain layers.
- **Key Implementations**:
  - `ProcessExternalProcessRunner`: Spawns CLI processes with timeout constraints (120s), handles cancellation by killing process trees, and redirects standard output stream line-by-line.
  - `ProcessWingetCommandRunner`: A winget CLI runner wrapping the external process runner.
  - `WingetTableParser`: Parses winget's tabular CLI stdout. Uses multi-language column header localization (EN, IT, FR, ES, DE) to support running on different system locales.
  - `WingetErrorClassifier`: Classifies CLI string outputs to map failures into structured `WingetErrorKind` enums.
  - `PowerShellWindowsUpdateService`: Connects to Windows Update Agent API via COM (`Microsoft.Update.Session`) inside base64 encoded PowerShell scripts, parsing returned JSON models.
  - `JsonWorkspaceStore` / `JsonSourcePreferenceStore`: Implements file-based JSON persistence in `%LOCALAPPDATA%\OnlyWinget\` with instance-scoped serialization.

### WinUI Presentation Layer (`OnlyWinget`)
- **Location**: `src/OnlyWinget`
- **Role**: Entry point of the application and the Graphical User Interface built on WinUI 3.
- **Dependencies**: Depends on Application, Domain, and Infrastructure layers.
- **Key Implementations**:
  - `AppComposition`: The static composition root that builds UI services and real infrastructure services, then constructs the `OnlyWingetApplication` instance.
  - `MainWindow`: Customs the title bar, configures Mica system backdrop, and hosts the navigation shell (`NavigationView`) with a page factory and page instance cache.
  - `TextResources`: Internal localization dictionary (no RESW/RESX). Supports English and Italian.
  - **Custom Controls**: Reusable layouts such as `OnlyWingetTable` (custom list-view grid table with tri-state selection header) and `StatePresenter` (loading/empty/error state visual transitions).

---

## 2. Concurrency & Synchronization Guidelines

- **Asynchronous Serialization**:
  Only one major operation (search, update check, installer run) can execute at a time. The `OnlyWingetApplication` class protects this using `Interlocked.CompareExchange` on the `ApplicationBusyState` field. Manual UI command disabling is used only as a secondary UX safety; the application layer is the definitive concurrency guard.
- **In-Memory Thread Safety**:
  Modifications to shared caches and dictionaries (e.g., `packageMetadata`) must be wrapped in standard C# `lock` synchronization statements.
- **File Persistence Safety**:
  Write/read operations in JSON stores (`JsonWorkspaceStore`, `JsonSourcePreferenceStore`) and app settings writes must be synchronized using instance-scoped `SemaphoreSlim(1,1)` locks.

---

## 3. Asynchronous Execution and Cancellation

- **Always Propagate CancellationTokens**:
  Every asynchronous method that supports cancellation must accept a `CancellationToken` parameter and forward it to all downstream async and process calls.
- **No CancellationToken.None**:
  Never pass `CancellationToken.None` into downstream operations within methods that accept a real token.
- **Process Cancellation**:
  The `ProcessExternalProcessRunner` must handle cancellation by killing the spawned process and its entire child process tree to prevent orphaned background processes.

---

## 4. COM Interop Guidelines

- **Windows Update COM Interop**:
  Avoid calling Windows Update COM APIs (`WUApiLib`) directly from the main WinUI STA (Single-Threaded Apartment) thread, as blocking COM operations will freeze the user interface. Run them inside background PowerShell processes using JSON output serialization.
- **Embed Interop Types**:
  If direct COM references are ever introduced to the C# projects, the assembly property `Embed Interop Types` must be set to `False` on the COM reference (e.g., `WUApiLib`) to avoid runtime `MissingMethodException`s. Alternatively, use late-binding dynamic instantiation (`Type.GetTypeFromProgID("Microsoft.Update.Session")`).
