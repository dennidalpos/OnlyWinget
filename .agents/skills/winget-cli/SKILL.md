---
name: winget-cli
description: "Develop OnlyWinget WinGet CLI invocation, source-aware package identity, parsing, cancellation and error classification."
---

# OnlyWinget WinGet integration

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Trace Application callers through ProcessWingetCommandRunner and ProcessExternalProcessRunner before changing invocation or result behavior.

- Use ArgumentList with exact package ID/source selection and the existing validators/parsers. Do not concatenate shell commands or invent unsupported JSON output flags.
- PackageIdentity equality includes case-insensitive ID and source. Source-specific search/resolve caches must preserve that identity.
- The Application workflow owns operation serialization; do not add an arbitrary parallel CLI pool.
- Read-only discovery does not require elevation. Source reset is a separate explicit destructive operation, not an automatic certificate-error workaround.
- Preserve completed, failed, cancelled and never-started batch outcomes. Cancellation does not prove a package operation was undone.
- HRESULT mappings follow Microsoft's return codes, including signed/unsigned representations. Native codes override localized heuristics, even when classified Unknown. Deterministic invalid-argument/source/ambiguity/hash/certificate/custom-installer failures do not start another package attempt.
- Uninstall delegates installed-match validation to WinGet with the original exact ID/source, without remote show or application source-enable checks. Installed-status absence requires native no-match; process/source/permission/timeout and unreadable/ambiguous success results fail preflight with diagnostics.

Use [commands](../onlywinget/references/commands.md) for offline verification. Live install/uninstall/source mutations require the intended packages and environment; they are not a documentation check.

Sources: [WinGet command documentation](https://learn.microsoft.com/en-us/windows/package-manager/winget/), [troubleshooting](https://learn.microsoft.com/en-us/windows/package-manager/winget/troubleshooting), [official return codes](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md), [manifest schemas](https://github.com/microsoft/winget-pkgs/tree/master/doc/manifest/schema).
