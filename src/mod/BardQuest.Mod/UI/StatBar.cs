using UnityEngine;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// A single progress bar shared by every "progress toward next" indicator (class rank, attribute XP): a
// 9-slice groove track with a tinted fill. Magnitude values (0–10) are drawn as PowerRings instead, so a
// bar here always means progress, never a level.
public static class StatBar
{
    public static VisualElement Build(BardQuestArt art, float frac, Color fill, float height = 14f)
    {
        float inset = Mathf.Max(2f, height * 0.18f);
        var track = new VisualElement
        {
            style = { height = height, overflow = Overflow.Hidden },
        };
        track.style.backgroundImage = new StyleBackground(Background.FromTexture2D(art.BarTrack()));

        // 9-slice the groove so only the middle channel stretches; the end caps stay fixed. Insets are
        // measured from the source art (like BardChrome) — retune when the final bar_track.png lands.
        track.style.unitySliceLeft = 130;
        track.style.unitySliceRight = 130;
        track.style.unitySliceTop = 30;
        track.style.unitySliceBottom = 30;
        track.style.unitySliceScale = height / 260f;

        track.Add(new VisualElement
        {
            style =
            {
                position = Position.Absolute,
                left = inset, top = inset, bottom = inset,
                width = Length.Percent(Mathf.Clamp01(frac) * 100f),
                backgroundColor = fill,
            },
        });
        return track;
    }
}
