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
    // Icons are large source PNGs shown small (heavy minification), so they need mipmaps or they alias into a
    // pixelated shimmer — mip: true generates the mip chain and uses trilinear filtering.
    public Texture2D ClassMedallion(PlayerClass cls) => Get("class_" + cls.ToString().ToLowerInvariant(), 0xD9A441, mip: true);
    public Texture2D RankBadge(Rank rank) => Get("rank_" + rank.ToString().ToLowerInvariant(), 0x4ADE80, mip: true);
    // The header's subrank row draws three leaf pips: a filled leaf for each attained subrank and an empty
    // (outline) leaf for the rest.
    public Texture2D RankLeafFull() => Get("rank_leaf_full", 0x4ADE80, mip: true);
    public Texture2D RankLeafEmpty() => Get("rank_leaf_empty", 0x3B4A3B, mip: true);
    public Texture2D AttributeIcon(Attribute a) => Get("attr_" + a.ToString().ToLowerInvariant(), 0x4ADE80, mip: true);
    public Texture2D Backdrop() => Get("backdrop", 0x0E2014);
    public Texture2D PanelFrame() => Get("panel_frame", 0x6B4A2F);
    public Texture2D ParchmentCard() => Get("card_parchment", 0xF2ECD8);
    public Texture2D BannerPrimary() => Get("banner_primary", 0xD9A441);
    public Texture2D BannerSecondary() => Get("banner_secondary", 0x6B4A2F);
    public Texture2D Logo() => Get("logo", 0x4ADE80, mip: true);
    public Texture2D Glow() => Get("glow", 0xFFF3C0);
    public Texture2D SelectGlow() => Get("select_glow", 0xFFD868);
    public Texture2D BeginGlow() => Get("begin_glow", 0xFFD868);

    // A 9-slice stat-bar groove. Currently unused (the header bars are drawn by XpBar); kept for a future
    // art-backed track. Dark placeholder channel until the PNG exists.
    public Texture2D BarTrack() => Get("bar_track", 0x2A1E12);

    // The plain wooden 9-slice border shared by the header and wave-list frames. Tinted placeholder until
    // the PNG lands.
    public Texture2D FrameWood() => Get("frame_wood", 0x6B4A2F);

    // Vine-and-flower ornaments overlaid on the wooden frame: the full cluster (used on the top corners),
    // the sparse variant (bottom corners), and a seamless vine runner tiled along the straight rails. A
    // missing file is invisible (transparent placeholder) rather than a tinted block.
    public Texture2D FrameCorner() => Get("frame_corner", 0x000000, 0x00);
    public Texture2D FrameCornerAlt() => Get("frame_corner_alt", 0x000000, 0x00);
    public Texture2D FrameVine() => Get("frame_vine", 0x000000, 0x00);

    // Wooden board that fills the interior behind the (hollow-centered) frame, so the header and wave
    // panels read as a textured plank rather than empty backdrop. Opaque placeholder until the PNG lands.
    public Texture2D WoodPanel() => Get("wood_panel", 0x5A3D24, 0xFF);

    private Texture2D Get(string name, uint placeholderRgb, byte placeholderAlpha = 0xC0, bool mip = false)
    {
        if (_cache.TryGetValue(name, out Texture2D? cached))
        {
            return cached;
        }

        Texture2D tex = Load(name, mip) ?? Placeholder(placeholderRgb, placeholderAlpha);
        _cache[name] = tex;
        return tex;
    }

    private Texture2D? Load(string name, bool mip)
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
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mip);
            if (!tex.LoadImage(ms.ToArray())) // LoadImage resizes to the PNG's dimensions (and builds mips if requested)
            {
                return null;
            }

            AlphaBleed(tex, mip);
            if (mip)
            {
                tex.filterMode = FilterMode.Trilinear;
            }

            return tex;
        }

        return null;
    }

    // Transparent PNG regions are typically stored as white (255,255,255,0); bilinear filtering then bleeds
    // that white into opaque edges as a light halo in-game (invisible in image viewers, which honor alpha).
    // Dilate the opaque edge colours a few pixels into the transparent region — alpha kept at 0 — so filtering
    // blends colour→colour with no halo. Fully-opaque textures are skipped, so the backdrop/wood panel cost
    // nothing. Bled pixels keep alpha 0, so each pass spreads exactly one pixel.
    private static void AlphaBleed(Texture2D tex, bool hasMips, int passes = 3)
    {
        int w = tex.width, h = tex.height;
        Color32[] p = tex.GetPixels32();

        bool anyTransparent = false;
        for (int i = 0; i < p.Length; i++)
        {
            if (p[i].a == 0)
            {
                anyTransparent = true;
                break;
            }
        }

        if (!anyTransparent)
        {
            return;
        }

        int[] dx = [1, -1, 0, 0, 1, 1, -1, -1];
        int[] dy = [0, 0, 1, -1, 1, -1, 1, -1];
        for (int pass = 0; pass < passes; pass++)
        {
            bool changed = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = (y * w) + x;
                    if (p[idx].a != 0)
                    {
                        continue;
                    }

                    for (int k = 0; k < 8; k++)
                    {
                        int nx = x + dx[k], ny = y + dy[k];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                        {
                            continue;
                        }

                        Color32 n = p[(ny * w) + nx];
                        if (n.a != 0)
                        {
                            p[idx] = new Color32(n.r, n.g, n.b, 0);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            if (!changed)
            {
                break;
            }
        }

        tex.SetPixels32(p);
        tex.Apply(hasMips);
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
