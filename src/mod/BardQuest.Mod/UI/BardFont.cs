using System.Reflection;

using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// The UI's fonts: a bespoke display face (Cinzel, embedded, OFL) for titles/headers/CTAs, with the
// runtime Arial as both the body face and the fallback if the embedded face cannot be realized at
// runtime. Realized once, lazily.
//
// Unity 6 dropped usable runtime TTF loading through the legacy Font pipeline (a `new Font(path)` renders
// as the default system face when assigned to `unityFont`). The face only rasterizes through TextCore:
// wrap the file-backed Font in a dynamic FontAsset and assign it via `unityFontDefinition`. Callers go
// through ApplyDisplay so they never have to branch on which path succeeded.
public static class BardFont
{
    private static bool _tried;
    private static FontAsset? _displayAsset;

    public static bool DisplayIsCustom => Ensure() != null;

    public static Font Body => field ??= LegacyRuntime();

    // Sets the ornate display face on an element: the embedded Cinzel SDF FontAsset when it realized,
    // otherwise the legacy Arial fallback (so titles stay legible even if the custom face fails to load).
    public static void ApplyDisplay(VisualElement element)
    {
        FontAsset? asset = Ensure();
        if (asset != null)
        {
            element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(asset));
        }
        else
        {
            element.style.unityFont = Body;
        }
    }

    private static FontAsset? Ensure()
    {
        if (!_tried)
        {
            _tried = true;
            _displayAsset = TryLoadEmbedded("Cinzel-SemiBold.ttf");
        }

        return _displayAsset;
    }

    private static Font LegacyRuntime()
        => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

    private static FontAsset? TryLoadEmbedded(string fileSuffix)
    {
        Assembly asm = typeof(BardFont).Assembly;
        foreach (string res in asm.GetManifestResourceNames())
        {
            if (!res.EndsWith(".fonts." + fileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream? s = asm.GetManifestResourceStream(res);
            if (s == null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return RealizeFontAsset(ms.ToArray());
        }

        return null;
    }

    // Writes the embedded TTF to a temp file (a dynamic FontAsset needs a file-backed source Font it can
    // re-read to rasterize new glyphs on demand) and builds a TextCore SDF FontAsset from it. Returns null
    // on any failure so ApplyDisplay falls back to Arial.
    private static FontAsset? RealizeFontAsset(byte[] ttf)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "BardQuest-Cinzel-SemiBold.ttf");
            if (!File.Exists(path) || new FileInfo(path).Length != ttf.Length)
            {
                File.WriteAllBytes(path, ttf);
            }

            var source = new Font(path);
            var asset = FontAsset.CreateFontAsset(source);
            return asset != null && asset.characterTable != null ? asset : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
