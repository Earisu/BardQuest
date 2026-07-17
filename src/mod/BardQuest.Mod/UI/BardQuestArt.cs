using System.Reflection;

using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Domain.Ratings;

using UnityEngine;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Mod.UI;

// Loads embedded UI PNGs to Texture2D by logical name, caching each. Any missing resource yields a small
// tinted placeholder texture so every screen renders before the bespoke art is produced.
public sealed class BardQuestArt
{
    private readonly Assembly _asm = typeof(BardQuestArt).Assembly;
    private readonly Dictionary<string, Texture2D> _cache = [];

    // The monster frame overlays the album art (real frames have a transparent center). Until the bespoke
    // frame PNG exists its placeholder must be fully transparent, or a translucent tint washes the album.
    public Texture2D MonsterFrame(MonsterType type) => Get("monster_" + type.ToString().ToLowerInvariant(), 0x6B4A2F, 0x00);
    public Texture2D ClassMedallion(PlayerClass cls) => Get("class_" + cls.ToString().ToLowerInvariant(), 0xD9A441);
    public Texture2D RankBadge(Rank rank) => Get("rank_" + rank.ToString().ToLowerInvariant(), 0x4ADE80);
    public Texture2D AttributeIcon(Attribute a) => Get("attr_" + a.ToString().ToLowerInvariant(), 0x4ADE80);
    public Texture2D Backdrop() => Get("backdrop", 0x0E2014);
    public Texture2D PanelFrame() => Get("panel_frame", 0x6B4A2F);
    public Texture2D ParchmentCard() => Get("card_parchment", 0xF2ECD8);
    public Texture2D BannerPrimary() => Get("banner_primary", 0xD9A441);
    public Texture2D BannerSecondary() => Get("banner_secondary", 0x6B4A2F);
    public Texture2D Logo() => Get("logo", 0x4ADE80);
    public Texture2D Glow() => Get("glow", 0xFFF3C0);

    private Texture2D Get(string name, uint placeholderRgb, byte placeholderAlpha = 0xC0)
    {
        if (_cache.TryGetValue(name, out Texture2D? cached))
        {
            return cached;
        }

        Texture2D tex = Load(name) ?? Placeholder(placeholderRgb, placeholderAlpha);
        _cache[name] = tex;
        return tex;
    }

    private Texture2D? Load(string name)
    {
        // Logical resource names are "<RootNamespace>.Art.<file>.png"; match by suffix to be robust to
        // the exact root namespace.
        string suffix = ".Art." + name + ".png";
        foreach (string res in _asm.GetManifestResourceNames())
        {
            if (!res.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream? s = _asm.GetManifestResourceStream(res);
            if (s == null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(ms.ToArray()) ? tex : null; // LoadImage resizes to the PNG's dimensions
        }

        return null;
    }

    private static Texture2D Placeholder(uint rgb, byte alpha)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var c = new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), alpha);
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = c;
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
