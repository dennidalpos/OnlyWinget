---
name: winui-code-review
description: "Review edited OnlyWinget WinUI XAML and C# for binding, MVVM, accessibility, theme, persistence and native ownership problems."
---

# OnlyWinget UI review

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Review the edited scope and its callers after compilation; list concrete findings with location, severity and user impact. Do not refactor working code solely to match a generic style.

- Accept the existing field-based ObservableProperty generators and UiCommand metadata. Use RelayCommand where already appropriate; business operations remain in Application.
- Check explicit changing-data binding modes, nullable paths, editor PropertyChanged updates, stable collections and single event wiring. Compiled templates need compatible types.
- Apply/save failures must retain drafts. After an accepted mutation, retry persistence only; block competing edits and navigation until saving succeeds.
- Every dialog needs a valid XamlRoot. Review reentrancy, close/light-dismiss behavior and unavailable-root failure paths; compilation does not verify those interactions.
- Check accessible IDs/names, keyboard behavior, explicit status text, theme resources and High Contrast. Severity must not rely only on color.
- Localize in EN/IT TextResources.cs with Localize; do not add RESW/RESX or ResourceLoader for this app.
- Async void belongs only to event handlers. Avoid blocking waits and heavy UI-thread work; review cancellation/disposal and dispatcher lifetimes.
- Check ArgumentList use, validated inputs and ownership before file/process/registry changes.
- OnlyWinget's typecheck does not automatically load the optional upstream WinUI analyzer. Report the tool actually executed.

[Detailed quality topics](references/quality-rules.md) are optional background. Apply the project conventions above to its generic examples. Keep unresolved interactive checks in the tracker.
