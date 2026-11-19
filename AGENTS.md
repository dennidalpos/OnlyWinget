<!-- secondbrain:repository:begin -->
Common directives applied: revision **1.5** — 2026-11-19. <!-- secondbrain:source global=BF1230F0D495F6838C89A2E7CAE002840CBC178B808D896A90A8F93357CABEFE workstation=2B4198D3D9F66B2EC4AD159DC7889F6C21AD64C5E025B99005D26122D6E5AEDB -->
## SecondBrain

Repository knowledge lives in the SecondBrain dossier; code, guides, and publishable assets stay here. This block is generated: write project rules outside it, because a sync replaces everything inside.

- Load the slim startup with MCP get_startup_context or the startup mode of the SecondBrain knowledge tool, and follow its references for detail. If the checkout is unreachable, report the dossier as unavailable and continue from the repository files.
- Write only to this repository's dossier `repos/<repo>/`, only through SecondBrain scripts or MCP write tools. Work folders come from the work tool's init mode; sessions, retention, and cleanup rules load from `sb prompt`.
- Commits, pushes, deletions, and publication require specific authorization.
<!-- secondbrain:repository:end -->

## Git workflow (push policy)

- The repository owner is an admin and changes are verified locally before any push. When a push is authorized, push directly to `main` and do not open a pull request. This applies only to pushes the user has authorized; it does not authorize committing or pushing on its own. If anything is unclear (failed checks, unexpected remote state, protected-branch rejection), stop and ask.

## Project maintenance

- Confirm documented behavior against the application, its resource labels and the scripts. Keep the English and Italian user guides in sync. Validate Mermaid diagrams when their flows change.
- Use the SDK pinned in `global.json` and a locked NuGet restore. Keep `CREDITS.md` in sync with package references, lockfiles and the self-contained payload when dependencies change.
- Run the Release gate in an isolated checkout because it clears that checkout's generated output. Keep local build evidence under `artifacts/` and landing payloads under `landing/build/`. Track the landing directory marker and keep generated downloads and backups out of Git.
- Treat a successful local gate, live host checks and publication as separate evidence. Check the exact setup and portable payload for required third-party licenses and notices before distributing it.
- Confirm changes to GitHub repository settings, community files and security policy with the owner. Verify the public state after applying confirmed settings.
