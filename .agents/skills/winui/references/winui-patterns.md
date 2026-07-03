# WinUI 3 Common Design & Architecture Patterns

This document describes the preferred architectural and design patterns for WinUI 3 development within the **OnlyWinget** codebase.

## 1. XAML and UI Thread Patterns
- **Strongly Typed Bindings (`x:Bind`)**: Always prefer `x:Bind` over `Binding`. It evaluates at compile time, is faster, and provides type-safety.
  ```xml
  <!-- Preferred -->
  <TextBlock Text="{x:Bind ViewModel.ItemName, Mode=OneWay}" />
  ```
- **Performance in Lists (`x:Phase`)**: Use `x:Phase` on heavy elements within a `DataTemplate` (under `ListView` or `GridView`) to allow progressive rendering.
- **Unloaded Cleanup**: Always decouple event handlers and dispose disposable resources in the `Unloaded` event of user controls or pages to prevent memory leaks.
  ```csharp
  private void OnUnloaded(object sender, RoutedEventArgs e)
  {
      this.Unloaded -= OnUnloaded;
      ViewModel.Cleanup(); // Remove handlers, clean bindings
  }
  ```

## 2. MVVM (Model-View-ViewModel) Pattern
We use the **CommunityToolkit.Mvvm** framework. Keep code-behind minimal and delegate business/presentation logic to ViewModels.

- **ObservableProperty**: Use the `[ObservableProperty]` source generator on private fields to generate properties that notify changes.
- **RelayCommand**: Use the `[RelayCommand]` attribute to automatically generate Command objects for XAML binding.
  ```csharp
  using CommunityToolkit.Mvvm.ComponentModel;
  using CommunityToolkit.Mvvm.Input;

  public partial class SettingsViewModel : ObservableObject
  {
      [ObservableProperty]
      private bool _isNotificationEnabled;

      [RelayCommand]
      private async Task SaveSettingsAsync()
      {
          // Async execution logic
      }
  }
  ```

## 3. Lifecycle, Threading, & Concurrency
- **Asynchronous Execution**: Never block the UI thread. Use `async` / `await` for all system, network, or file I/O operations.
- **Dispatcher Queue**: WinUI controls must be updated from the main UI thread. If you are returning from a background thread or callback, marshall the call back using the `DispatcherQueue`:
  ```csharp
  DispatcherQueue.TryEnqueue(() =>
  {
      this.StatusText.Text = "Completed!";
  });
  ```
- **Serialization of Asynchronous Commands**: Avoid overlapping execution. Serialize operations in the Application layer (e.g. `OnlyWingetApplication`). Do not rely solely on disabling UI buttons.
- **Cancellation**: Pass an explicit `CancellationToken` to all async operations that can be cancelled. Never use `CancellationToken.None` for work meant to support cancellation.

## 4. One-Way Dependency Flow
Dependencies must flow strictly from Presentation down to Application and Domain layers:
```
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```
- WinUI pages/controls must not leak details to the Domain.
- Services interacting with OS, PowerShell, or external CLI processes (such as Winget or Windows Update) must be abstracted under interfaces (e.g., `ISystemCapabilityService`) in the Application layer, with actual concrete implementations in the Infrastructure layer.

## 5. Dual-Language Localization (EN & IT)
- Always localise user-facing strings.
- Place resource keys in `src/OnlyWinget/TextResources.cs` or resw files.
- Support both English (`en-US`) and Italian (`it-IT`).
