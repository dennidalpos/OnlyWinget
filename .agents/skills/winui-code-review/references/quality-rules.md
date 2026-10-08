# Detailed OnlyWinget review topics

Read the edited scope and actual consumers; generic library examples are not repository contracts.

## State and binding

Check feature activation/deactivation, handler disposal and DispatcherQueue callbacks after a window closes. Avoid replacing observable collections. Verify binding modes for mutable data, editor PropertyChanged behavior and command enabled-state updates during async work.

Apply/save and navigation are one consistency boundary: keep rejected drafts, lock accepted-unsaved edits, retry persistence without repeating the mutation and preserve selection when navigation is rejected. Do not treat an unavailable dialog root as consent.

## Performance and lifetime

Use ListView/ItemsStackPanel virtualization and shared table sizing. Move CPU-bound work off the UI thread where needed; use asynchronous I/O directly. Review cancellation source ownership and file/native/COM disposal. Never wait synchronously for UI-dependent async work.

## Ownership and security

Use ArgumentList and existing validators. Scope process/window operations to the actual executable/PID/HWND and preserve unrelated installations/pickers. Preserve personal stores and recovery evidence; matching a filename is not permission to delete data.

The app is unpackaged/asInvoker; Package.appxmanifest capability advice does not apply. Registry cleanup requires matching legacy metadata and command. No credentials or raw sensitive transcripts belong in reports.

## Theme, accessibility and localization

Use semantic controls, stable AutomationIds, names for unlabeled controls, keyboard focus and explicit status text. Read actual Light/Dark/High Contrast behavior separately from compile checks.

Use TextResources.cs/Localize EN/IT, including dialog titles, operation outcomes and automation labels. Validate literal/dynamic keys and composite formats. RESW/RESX, x:Uid and ResourceLoader are not this application's localization system.

Sources: [WinUI accessibility](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility), [dialogs](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs), [deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/).
