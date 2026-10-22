# OnlyWinget view patterns

Inspect existing views under `src/OnlyWinget/Features/` and primitives under `src/OnlyWinget/DesignSystem/` and `src/OnlyWinget/Controls/` before adding new abstractions.

- **ViewModels (`src/OnlyWinget/Presentation/FeatureViewModel.cs`)**: expose stable `ObservableCollection<T>` properties (updated via `ObservableCollectionExtensions`) and refresh through `OnlyWingetApplication.StateChanged` marshaled onto `DispatcherQueue`. Attach/detach event handlers cleanly across page navigation and window close.
- **MVVM & commands**: field-based `[ObservableProperty]` generators and typed `UiCommand` definitions (`src/OnlyWinget.Application/Presentation/UiCommand.cs`, rendered by `DesignSystem/Commands/OnlyWingetCommandBar`) are standard. `[RelayCommand]` is used where appropriate.
- **Bindings & validation**: use `x:Bind` with explicit `Mode=OneWay` or `Mode=TwoWay` for mutable state (`OneTime` only for immutable command/row metadata). Editor `TextBox` bindings use `UpdateSourceTrigger=PropertyChanged` with `ValidatedField` so edits revalidate immediately.
- **Tables (`src/OnlyWinget/Controls/OnlyWingetTable.xaml`)**: owns `ListView` + `ItemsStackPanel` virtualization, shared column widths, keyboard/checkbox selection, mixed select-all, filtering (`OnlyWingetTableFilterEngine`) and UI Automation labels. Do not replace it with `ItemsRepeater`.
- **Drafts & navigation guards (`src/OnlyWinget/Presentation/IPendingNavigationGuard.cs`)**: code-behind coordinates attached flyouts (`ShowAt`), `ContentDialog` `XamlRoot` assignment and route guards. Dirty preset drafts survive flyout dismissal; failed validation or save never clears a draft or permits navigation.
- **Localization & theming**: use `src/OnlyWinget/TextResources.cs` and `Services/LocalizeExtension.cs` (`Localize`) for EN/IT strings, `DesignSystem/Resources/*.xaml` styles and `{ThemeResource ...}` brushes. Keep status severity expressed in text as well as color.
