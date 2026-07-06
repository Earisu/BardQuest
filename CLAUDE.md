# BardQuest

An in-YARG Unity mod delivering RPG-style quest progression against your real YARG scores.

## Layout

- `src/BardQuest.Domain` — pure logic (net10.0;netstandard2.1). No Unity, no I/O.
- `mod/BardQuest.Mod` — the in-game mod (netstandard2.1). Builds only with `-p:YargManaged=<sandbox Managed>`.
- `installer/BardQuest.Installer` — standalone Cecil patcher (net10.0). Single seam: one call injected at `MainMenu.OnEnable`.
- `lib/YARG.Core` — vendored source submodule (enums shared with the game).

## Build & run (sandbox)

- `dotnet build BardQuest.slnx` — builds everything (domain, mod, installer, tests). The mod compiles against the sandbox YARG assemblies by default (`yarg-sandbox/`); override with `-p:YargManaged=<path-to-a-YARG-Managed-folder>`.
- `bash scripts/deploy-sandbox.sh` — builds the mod + installer and installs into `yarg-sandbox/`.
- Verification is user-driven, in-game, against the sandbox copy only.

## Conventions

- `docs/`, `.superpowers/`, `.claude/` are gitignored.
- **Never** modify YARG's real persistent data dir or logs; operate only on `yarg-sandbox/`. Trigger rescans via YARG's in-game Settings.

## Style

- Code style enforced via root `.editorconfig`; run `dotnet format` before committing.
