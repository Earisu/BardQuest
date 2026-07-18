using UnityEngine;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// The persistent shell header: the BardQuest logo centered and large, with a per-screen title beneath it.
// Owned by the canvas and never rebuilt, so the logo holds its position while only the content region swaps.
// There is no back button — Back is the Red nav action everywhere (shown in YARG's footer help bar).
public sealed class AppHeader
{
    private readonly Label _title;

    public VisualElement Root { get; }

    public AppHeader(BardQuestArt art)
    {
        Root = new VisualElement
        {
            style =
            {
                flexShrink = 0, height = 288, position = Position.Relative,
                alignItems = Align.Center, justifyContent = Justify.Center, paddingTop = 20,
            },
        };

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
