using BardQuest.Domain.Progression;

using UnityEngine;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Mod.UI;

// Presentation constants for the BardQuest UI: the approved enchanted-forest palette plus display names
// and per-axis colors. Pure presentation — the Domain layer is name/color agnostic.
public static class BardTheme
{
    public static readonly Color32 Nightwood = new(0x06, 0x0F, 0x0A, 0xFF);
    public static readonly Color32 Mossdeep = new(0x0E, 0x20, 0x14, 0xF2);
    public static readonly Color32 Glowmoss = new(0x4A, 0xDE, 0x80, 0xFF);
    public static readonly Color32 Gilt = new(0xD9, 0xA4, 0x41, 0xFF);
    public static readonly Color32 OldWood = new(0x6B, 0x4A, 0x2F, 0xFF);
    public static readonly Color32 Parchment = new(0xF0, 0xF5, 0xE8, 0xFF);
    public static readonly Color32 Card = new(0xF2, 0xEC, 0xD8, 0xFF);
    public static readonly Color32 FeyMagic = new(0xB0, 0x6C, 0xF7, 0xFF);
    public static readonly Color32 Ember = new(0xFF, 0x8A, 0x4C, 0xFF);

    public static string ClassName(PlayerClass c) => c.ToString();

    public static string Roman(int subrank) => subrank switch
    {
        0 => "I",
        1 => "II",
        _ => "III",
    };

    public static string AxisName(Attribute a) => a.ToString();

    public static string PaceName(QuestPace p) => p.ToString();

    public static Color AxisColor(Attribute a) => a switch
    {
        Attribute.Strength => new Color32(0xE0, 0x5A, 0x4C, 0xFF),
        Attribute.Endurance => new Color32(0xFF, 0x8A, 0x4C, 0xFF),
        Attribute.Technique => new Color32(0x6C, 0xB6, 0xF7, 0xFF),
        Attribute.Agility => new Color32(0x4A, 0xDE, 0x80, 0xFF),
        _ => new Color32(0xD9, 0xA4, 0x41, 0xFF), // Dexterity
    };
}
