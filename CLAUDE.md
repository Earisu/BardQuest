# BardQuest

An in-YARG Unity mod delivering RPG-style quest progression against your real YARG scores.

## Layout

- `src/mod/BardQuest.Domain` — pure logic (net10.0;netstandard2.1). No Unity, no I/O.
- `src/mod/BardQuest.Mod` — the in-game mod (netstandard2.1). Builds only with `-p:YargManaged=<sandbox Managed>`.
- `src/installer/BardQuest.Updater` — Avalonia GUI app (net10.0) to install/update/remove the mod, over a `BardQuest.Updater.Core` library (Cecil patcher, YARG-install discovery, GitHub release check, YARG-version compatibility gate). The mod bakes `BardQuestModVersion`/`BardQuestYargTarget` markers (via `-p:ModVersion`/`-p:YargTarget`); the updater reads them to show versions and block installing a build into a mismatched YARG. Also keeps a headless `install | patch | restore` CLI for `deploy-sandbox.sh`/CI.
- `lib/YARG.Core` — vendored source submodule (enums shared with the game).

## Build & run (sandbox)

- `dotnet build BardQuest.slnx` — builds everything (domain, mod, installer, tests). The mod compiles against the sandbox YARG assemblies by default (`yarg-sandbox/`); override with `-p:YargManaged=<path-to-a-YARG-Managed-folder>`.
- Tests without YARG (e.g. CI): `dotnet test tests/BardQuest.Updater.Tests` — targets the test project only, so it needs no game assemblies. Only the mod build (and a full-solution build) require YARG.
- `bash scripts/deploy-sandbox.sh` — builds the mod + installer and installs into `yarg-sandbox/`.
- `dotnet run --project src/installer/BardQuest.Updater` — launches the GUI installer/updater. It discovers YARG installs from the YARC launcher (`<LocalAppData>/YARC/YARG Installs`), or takes a manually chosen Managed folder.
- Verification is user-driven, in-game, against the sandbox copy only.

## Conventions

- `docs/`, `.superpowers/`, `.claude/` are gitignored.
- **Never** modify YARG's real persistent data dir or logs; operate only on `yarg-sandbox/`. Trigger rescans via YARG's in-game Settings.

## Style

- **Clean architecture, one public type per file.** Filename matches the type name; no god-files or colocated helper classes/enums. Keep each class to one responsibility and respect the layering (Domain pure/no-I/O; Mod does runtime binding + I/O; installer separate). When editing a file that still bundles multiple public types, split it as part of the change.
- Code style enforced via root `.editorconfig` (Microsoft's documented .NET conventions, severities raised to `warning` so `dotnet format` actually applies them).
- Before committing, run `dotnet format style`, `dotnet format analyzers`, and `dotnet format whitespace` against each project individually (`src/mod/BardQuest.Domain`, `src/mod/BardQuest.Mod`, `src/installer/BardQuest.Updater`, `tests/BardQuest.Updater.Tests`) — never against `BardQuest.slnx` or `lib/YARG.Core`, since solution-wide formatting would also rewrite the vendored submodule.
- Rider users: `BardQuest.sln.DotSettings` (committed) mirrors this in ReSharper/Rider's own settings layer.
