# Theme and accessibility review

Use the current DesignSystem dictionaries and MainWindow.ApplyTheme as the repository examples. Preserve existing Light/Dark/High Contrast behavior when adjusting a view.

- Use ThemeResource at changing-theme usage sites. Check the key/type in the actual dictionary instead of assuming a brush exists.
- When introducing a custom theme key, ensure it resolves in every supported theme. An empty HighContrast dictionary cannot supply a key defined only in Light/Dark; retain a valid fallback or provide system-aware resources.
- High Contrast must preserve readable foreground/background pairs, focus and selection. Prefer platform resources and do not disable automatic adjustment to hide a rendering problem.
- Reuse existing styles and base styles. Avoid replacing a full ControlTemplate for a brush or spacing adjustment.
- Runtime theme changes follow the existing window/settings path. Validate real rendered controls, including disabled/selected/error states, when the task changes visuals.
- Keep status meaning in translated text and accessible names, independently of color.

Sources: [XAML theme resources](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources), [High Contrast themes](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes).
