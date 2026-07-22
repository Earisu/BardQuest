using UnityEngine;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// A rounded progress bar drawn from plain elements (no art): a dark pill track with a colored fill and a
// glassy sheen over the fill's top half. Used for the header's class-rank and attribute-XP bars. frac in [0,1].
public static class XpBar
{
    public static VisualElement Build(float frac, Color fill, float height = 16f)
    {
        const float border = 2f;
        float radius = height / 2f;
        // The fill sits inside the track's border, so its corner radius must be the track's INNER radius
        // (outer minus the border width) to nest cleanly against the rounded rail instead of overhanging it.
        float innerRadius = Mathf.Max(0f, radius - border);
        var gold = new Color(0xAE / 255f, 0x83 / 255f, 0x34 / 255f, 1f);
        var track = new VisualElement
        {
            style =
            {
                height = height, overflow = Overflow.Hidden,
                backgroundColor = new Color(0f, 0f, 0f, 0.45f),
                borderTopLeftRadius = radius, borderTopRightRadius = radius,
                borderBottomLeftRadius = radius, borderBottomRightRadius = radius,
                borderTopWidth = border, borderBottomWidth = border, borderLeftWidth = border, borderRightWidth = border,
                borderTopColor = gold, borderBottomColor = gold, borderLeftColor = gold, borderRightColor = gold,
            },
        };

        var fillEl = new VisualElement
        {
            style =
            {
                position = Position.Absolute, left = 0, top = 0, bottom = 0,
                width = Length.Percent(Mathf.Clamp01(frac) * 100f),
                backgroundColor = fill,
                borderTopLeftRadius = innerRadius, borderTopRightRadius = innerRadius,
                borderBottomLeftRadius = innerRadius, borderBottomRightRadius = innerRadius,
                overflow = Overflow.Hidden,
            },
        };

        // Glassy sheen: a translucent white cap over the top ~45% of the fill, rounded to match the fill so
        // the fill reads like a lit pill of liquid rather than a flat block.
        fillEl.Add(new VisualElement
        {
            style =
            {
                position = Position.Absolute, left = 0, right = 0, top = 0, height = Length.Percent(45),
                backgroundColor = new Color(1f, 1f, 1f, 0.30f),
                borderTopLeftRadius = innerRadius, borderTopRightRadius = innerRadius,
            },
        });

        track.Add(fillEl);
        return track;
    }
}
