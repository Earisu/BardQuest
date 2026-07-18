using UnityEngine;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// The persistent shell header: a red YARG-style back button pinned upper-left, the BardQuest logo
// centered and large, and a per-screen title beneath it. Owned by the canvas and never rebuilt, so the
// logo and back button hold their position while only the content region swaps.
public sealed class AppHeader
{
    private readonly Label _title;

    public VisualElement Root { get; }

    public AppHeader(BardQuestArt art, Action onBack)
    {
        Root = new VisualElement
        {
            style =
            {
                flexShrink = 0, height = 288, position = Position.Relative,
                alignItems = Align.Center, justifyContent = Justify.Center, paddingTop = 20,
            },
        };

        var back = new VisualElement
        {
            style =
            {
                position = Position.Absolute, left = 58, top = 42, width = 84, height = 84,
                backgroundColor = new Color(0.62f, 0.20f, 0.17f), // red plate, matches YARG's red BACK
                borderTopLeftRadius = 42, borderTopRightRadius = 42, borderBottomLeftRadius = 42, borderBottomRightRadius = 42,
                borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
                borderTopColor = new Color(0.30f, 0.09f, 0.07f), borderBottomColor = new Color(0.30f, 0.09f, 0.07f),
                borderLeftColor = new Color(0.30f, 0.09f, 0.07f), borderRightColor = new Color(0.30f, 0.09f, 0.07f),
                alignItems = Align.Center, justifyContent = Justify.Center,
            },
        };

        // A left chevron drawn from two borders (right+bottom) rotated 135°, so it renders regardless of
        // which glyphs the runtime font ships (the ❮ ornament tofu'd on the fallback face).
        var chevron = new VisualElement
        {
            style =
            {
                width = 24, height = 24, marginLeft = 8,
                borderRightWidth = 8, borderBottomWidth = 8,
                borderRightColor = Color.white, borderBottomColor = Color.white,
                rotate = new Rotate(new Angle(135f, AngleUnit.Degree)),
            },
        };
        back.Add(chevron);
        back.RegisterCallback<ClickEvent>(_ => onBack());
        Root.Add(back);

        Root.Add(new Image { image = art.Logo(), style = { width = 240, height = 240 } });

        _title = new Label
        {
            style =
            {
                color = (Color)BardTheme.Gilt, fontSize = 42,
                letterSpacing = 4, unityFontStyleAndWeight = FontStyle.Bold, marginTop = -8,
            },
        };
        BardFont.ApplyDisplay(_title);
        Root.Add(_title);
    }

    public void SetTitle(string title) => _title.text = title.ToUpperInvariant();
}
