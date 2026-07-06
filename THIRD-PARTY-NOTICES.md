# Third-party notices

The [MIT license](LICENSE) in this repository covers BardQuest's own code:
`src/BardQuest.Domain`, `mod/BardQuest.Mod`, `installer/BardQuest.Installer`,
`tests/`, and `scripts/`. It does not cover the third-party components below.

## YARG.Core (`lib/YARG.Core`)

Vendored as a git submodule pointing at the unmodified upstream repository
[YARC-Official/YARG.Core](https://github.com/YARC-Official/YARG.Core),
licensed under the **GNU Lesser General Public License v3.0**. Its license
text travels with the submodule at `lib/YARG.Core/LICENSE`.

BardQuest builds this submodule's source into `YARG.Core.dll` and ships that
build alongside its own DLLs. Per LGPL-3.0: the source is the unmodified
public upstream repository (linked above, pinned to the submodule's commit);
the compiled DLL is a separate, swappable file — nothing in the mod or
installer statically links or merges it into another assembly, so it can be
freely replaced with an independently rebuilt version.

## YARG (the game)

BardQuest's mod and installer reference the game's own assemblies (e.g.
`Assembly-CSharp.dll`) as build-time-only, non-redistributed references
(`Private="false"` in the mod's `Refs.props`) against a YARG install already
present on the user's machine. YARG itself — also LGPL-3.0
([YARC-Official/YARG](https://github.com/YARC-Official/YARG)) — is never
copied, modified, or redistributed by this project.

## Mono.Cecil (`installer/BardQuest.Installer`)

[jbevain/cecil](https://github.com/jbevain/cecil), MIT licensed, used by the
installer to patch the single seam call into `MainMenu.OnEnable`.
