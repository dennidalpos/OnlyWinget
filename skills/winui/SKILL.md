---
name: winui
description: Official developer skill for WinUI 3, Windows App SDK, Fluent Design UI layout, MVVM data binding, XAML controls, accessibility, and Native AOT / Trimming compilation rules.
---

# WinUI 3 & Windows App SDK Developer Skill

This skill provides guidelines and specifications for developing Windows desktop applications with **WinUI 3** and the **Windows App SDK**.

Source: [Microsoft Learn - WinUI 3 Documentation](https://learn.microsoft.com/windows/apps/winui/winui3/)

## 1. WinUI 3 & MVVM Architecture

- **UI Framework**: WinUI 3 / Windows App SDK (`Microsoft.UI.Xaml`).
- **MVVM Framework**: `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
- **Data Binding**: Prefer `x:Bind` over `Binding` for type safety, performance, and compile-time validation. ViewModels bound via `x:Bind` must be declared `public`.
- **Messenger**: Use `WeakReferenceMessenger` for decoupled view-model communication.

## 2. Layout & Fluent Design

- **Grid Spacing**: Use Grid rows/columns and `Margin`/`Padding` matching 4px/8px Fluent Design grid increments.
- **Controls**: Use standard WinUI 3 Fluent controls (`Button`, `TextBox`, `ToggleSwitch`, `InfoBar`, `NavigationView`, `ItemsRepeater`).
- **Theming**: Support Light, Dark, and High Contrast system themes via `{ThemeResource}` brushes.

## 3. Trimming & Native AOT Compatibility

- **Static Declarations**: Keep DTOs and dynamic JSON models statically typed with `[property: JsonPropertyName(...)]`.
- **Suppress Warnings**: Apply `[UnconditionalSuppressMessage]` with clear justification when invoking Win32/COM dynamic APIs protected by try-catch fallbacks.
