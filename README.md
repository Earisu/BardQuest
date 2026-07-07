# BardQuest

An in-YARG Unity mod delivering RPG-style quest progression against your real YARG scores.

## License

BardQuest's own code is licensed under the [MIT License](LICENSE). It builds
against and vendors third-party components under their own licenses — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Releasing

CI runs on every push/PR (`.github/workflows/ci.yml`): the YARG-free unit tests plus a
mod compile gate that downloads YARG and builds the mod against it. Releases are cut by
pushing tags — the mod and the updater are versioned and released independently:

- **Mod** — push a `mod-v*` tag (e.g. `mod-v1.2.0`). Builds the mod against the pinned
  YARG and publishes a Release with `bardquest-mod-<version>.zip` (the three mod DLLs).
- **Updater** — push an `updater-v*` tag (e.g. `updater-v0.3.0`). Publishes self-contained
  builds for Windows, Linux, and macOS (wrapped in a `.app` bundle) as zips — each zip holds
  the full self-contained app (runnable with no .NET install).

### Which YARG the mod targets

The pinned YARG version is a single line in `mod/BardQuest.Mod/Refs.props`
(`<YargTarget>`). It is baked into the mod DLL and read back by CI to choose which YARG to
download. To target a new YARG, edit that one line in a PR (fixing any mod code the new
YARG's API requires in the same PR). For a one-off build against a different YARG, branch,
edit the line, and push a `mod-v*` tag.

### macOS: unsigned app

The macOS updater is unsigned, so the first launch is blocked by Gatekeeper. Right-click
`BardQuest Updater.app` → **Open** → **Open** to run it (only needed once).

### Automatic updates (Windows & macOS)

After the first install, BardQuest keeps itself up to date in the background.
The updater registers a small login item that runs quietly in the system tray
(Windows) / menu bar (macOS). When a new mod release is published, it downloads
and applies the update automatically the next time it is safe — that is, when
YARG is closed and the release matches your installed YARG version. If an
update needs your attention (for example, it targets a different YARG version),
the tray flags it and clicking it opens the updater.

This is controlled by a single checkbox in the updater — **"Keep BardQuest up
to date automatically"** — which is on by default after your first install.
Uncheck it to remove the login item and stop background updates; you can still
update manually from the updater window at any time. (Linux has no background
tray; use the updater window to update.)
