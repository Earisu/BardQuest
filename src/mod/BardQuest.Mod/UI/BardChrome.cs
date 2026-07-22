using UnityEngine;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// Applies bespoke 9-slice art as element chrome (background image + slice insets). Each inset clears the
// source PNG's decorative corners/ends (extents measured from the art), so only the plain middle bands
// stretch to the element. unitySliceScale renders the fixed corners at a sane on-screen size.
public static class BardChrome
{
    // Ornate wood-and-leaf border over an opaque parchment interior. Source 1430x1071; the music-note
    // corner blocks and leaf clusters reach ~210px in, so a 210px inset clears them. Scale 0.55 renders
    // each corner ~115px; the thin wood edges become ~20-30px bands, and the parchment center stretches.
    public static void Panel(VisualElement e, BardQuestArt art)
        => Apply(e, art.PanelFrame(), 210, 210, 210, 210, 0.55f);

    // Parchment sheet. Source 850x1155; dark border + gold rule + corner flourish within ~90px.
    public static void Parchment(VisualElement e, BardQuestArt art)
        => Apply(e, art.ParchmentCard(), 90, 90, 90, 90, 0.5f);

    // The plain wooden 9-slice border (header + wave list). Source 232x224 with rounded corners; a ~70px
    // inset clears the rounded corner so only the straight rails stretch, and the scale renders the rail thin.
    public static void FrameWood(VisualElement e, BardQuestArt art)
        => Apply(e, art.FrameWood(), 70, 70, 70, 70, 0.7f);

    // Gold action plate. Source 1950x435; gem end-caps ~260px, gold rail ~60px. The caller's height sets
    // the scale so the caps keep their aspect and only the middle stretches horizontally to fit.
    public static void BannerPrimary(VisualElement e, BardQuestArt art, float height)
        => Apply(e, art.BannerPrimary(), 260, 60, 260, 60, height / 435f);

    public static void BannerSecondary(VisualElement e, BardQuestArt art, float height)
        => Apply(e, art.BannerSecondary(), 260, 60, 260, 60, height / 435f);

    private static void Apply(VisualElement e, Texture2D tex, int left, int top, int right, int bottom, float scale)
    {
        e.style.backgroundImage = new StyleBackground(Background.FromTexture2D(tex));
        e.style.unitySliceLeft = left;
        e.style.unitySliceTop = top;
        e.style.unitySliceRight = right;
        e.style.unitySliceBottom = bottom;
        e.style.unitySliceScale = scale;
    }
}
