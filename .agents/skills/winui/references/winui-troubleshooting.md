# OnlyWinget troubleshooting

Read the actual command output and affected source before applying a generic fix.

- NU1004 after RID changes: use the repository lockfile toolchain, not manual lockfile edits or disabling locked restore.
- XAML compiler/type errors: inspect generated diagnostics, namespace/type accessibility and referenced package versions; do not upgrade blindly or add a second UI framework.
- Stale view content: inspect binding modes, collection identity, dispatcher refresh and handler lifetime. OneTime is valid for fixed command/log-entry data.
- Editor failure: retain the draft, distinguish mutation from save failure and retry persistence only after an accepted mutation. Do not discard fields to hide an error.
- Dialog/picker errors: validate XamlRoot, reentrancy and native HWND ownership. A dialog root/build success is not interactive proof.
- Launch failure: resolve the real self-contained output path through repository scripts and check actual process lifetime. This app has WindowsPackageType=None; introducing MSIX identity is not a repair.
- WinApp/tool absence: it blocks only the requested tool-dependent workflow. Compilation and deterministic tests remain available.
- Log I/O failure: inspect surfaced LastError and settings filters without deleting personal logs or clearing Activity history.

Use [commands](../../onlywinget/references/commands.md) for relevant probes. Preserve original output and report validation limits.
