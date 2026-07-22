<!-- src/mod/BardQuest.Mod/Art/README.md -->
# BardQuest UI art

PNGs here are embedded into the mod and loaded by `BardQuestArt`. Generate them from the prompts in
`docs/superpowers/specs/2026-07-13-phase3-quest-ui-design.md` §9. Until a file exists, `BardQuestArt`
draws a tinted placeholder, so the UI is usable without it.

Expected filenames (all lowercase):
- `logo.png`, `app_icon.png`, `backdrop.png` (1920x1080), `panel_frame.png`, `card_parchment.png`
- `banner_primary.png`, `banner_secondary.png`
- Monster frames: `monster_regular.png`, `monster_elite.png`, `monster_boss.png`, `monster_rare.png`
- Class medallions: `class_busker.png`, `class_minstrel.png`, `class_troubadour.png`, `class_bard.png`, `class_skald.png`, `class_legendweaver.png`
- Shield rank badges: `rank_f.png` … `rank_sss.png` (`f,e,d,c,b,a,s,ss,sss`; shield-shaped)
- Subrank leaf pips: `rank_leaf_full.png` (attained) + `rank_leaf_empty.png` (not yet); the header draws three, filled up to the current subrank
- Attribute icons: `attr_strength.png`, `attr_endurance.png`, `attr_technique.png`, `attr_agility.png`, `attr_dexterity.png`
- Stat-bar groove: `bar_track.png` (~1950×260, prompt in `docs/superpowers/specs/2026-07-17-hub-duel-redesign-design.md`)
- Frame set (header + wave list), layered so the plain border 9-slices while the organic art rides on top:
  - `frame_wood.png` (plain rectangular wooden border, uniform rails, transparent hollow center, 9-sliceable)
  - `frame_corner.png` (top-left vine + flower cluster; mirrored onto all four corners — used on the top corners)
  - `frame_corner_alt.png` (sparse top-left vine, no flowers — used on the bottom corners)
  - `frame_vine.png` (seamless horizontal vine runner, tiled along the rails; rotated in code for the sides)
- Wood panel: `wood_panel.png` (opaque wooden board; fills the interior behind `frame_wood`; seamless/tileable)
