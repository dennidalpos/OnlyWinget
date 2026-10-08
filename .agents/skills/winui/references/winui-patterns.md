# OnlyWinget view patterns

Use the existing neighboring feature and DesignSystem implementation before adding another abstraction.

- Feature ViewModels expose stable observable collections and refresh through the workflow StateChanged event/dispatcher. Respect page reactivation when attaching/detaching handlers.
- Field-based ObservableProperty generators and typed UiCommand definitions are established conventions. RelayCommand is used where appropriate, not for every operation.
- Use compiled bindings when supported; choose OneWay/TwoWay for changing data. Editor TextBox bindings use PropertyChanged so source edits revalidate immediately.
- OnlyWingetTable owns ListView/ItemsStackPanel virtualization, shared widths, selection, keyboard and UI Automation behavior. Do not replace it with a generic repeater/template.
- View-specific code coordinates attached flyouts, dialogs and route guards. Dirty drafts survive dismissal; failed apply/save cannot clear them or authorize navigation.
- AttachedFlyout needs explicit ShowAt. ContentDialog needs the correct XamlRoot and same-window dialog coordination; log clear uses inline InfoBar confirmation.
- Use TextResources.cs/Localize EN/IT, existing text styles and ThemeResource brushes. Preserve meaningful accessible names and explicit status text.
- Keep native lifetime/cancellation and storage in their existing service/Application boundaries. Never block the UI waiting for asynchronous work.

Examples/templates must be adapted to the current project instead of importing their own packaging, storage or resource conventions.
