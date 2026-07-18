<!-- src/mod/BardQuest.Mod/Art/README.md -->
# BardQuest UI art

PNGs here are embedded into the mod and loaded by `BardQuestArt`. Generate them from the prompts in
`docs/superpowers/specs/2026-07-13-phase3-quest-ui-design.md` §9. Until a file exists, `BardQuestArt`
draws a tinted placeholder, so the UI is usable without it.

Expected filenames (all lowercase):
- `logo.png`, `app_icon.png`, `backdrop.png` (1920x1080), `panel_frame.png`, `card_parchment.png`
- Hub header logo mark: `logo_mark.png` (small, text-less BardQuest logo; square, transparent)
- `banner_primary.png`, `banner_secondary.png`
- Monster frames: `monster_regular.png`, `monster_elite.png`, `monster_boss.png`, `monster_rare.png`
- Class medallions: `class_busker.png`, `class_minstrel.png`, `class_troubadour.png`, `class_bard.png`, `class_skald.png`, `class_legendweaver.png`
- Rank badges: `rank_f.png` … `rank_sss.png` (`f,e,d,c,b,a,s,ss,sss`)
- Attribute icons: `attr_strength.png`, `attr_endurance.png`, `attr_technique.png`, `attr_agility.png`, `attr_dexterity.png`
