using BardQuest.Domain.Progression;

using UnityEngine;
using UnityEngine.UIElements;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Mod.UI;

// A five-axis radar drawn with Painter2D: an outer pentagon guide plus a filled polygon of the current
// levels (0..10). Axis order is fixed (Strength top, clockwise) so it reads consistently.
public sealed class PentagonRadar : VisualElement
{
    private static readonly Attribute[] Order =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    private readonly float[] _levels = new float[5];

    public PentagonRadar()
    {
        style.width = 160;
        style.height = 160;
        generateVisualContent += OnGenerate;
    }

    public void SetLevels(IReadOnlyDictionary<Attribute, AttributeState> axes)
    {
        for (int i = 0; i < Order.Length; i++)
        {
            _levels[i] = axes.TryGetValue(Order[i], out AttributeState s) ? Mathf.Clamp01(s.Level / 10f) : 0f;
        }

        MarkDirtyRepaint();
    }

    private void OnGenerate(MeshGenerationContext ctx)
    {
        float w = contentRect.width, h = contentRect.height;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var center = new Vector2(w / 2f, h / 2f);
        float radius = (Mathf.Min(w, h) / 2f) - 8f;
        Painter2D p = ctx.painter2D;

        // Outer guide pentagon.
        p.strokeColor = new Color(1f, 1f, 1f, 0.25f);
        p.lineWidth = 1.5f;
        TracePolygon(p, center, radius, null);
        p.Stroke();

        // Filled level polygon.
        p.fillColor = new Color(BardTheme.Glowmoss.r / 255f, BardTheme.Glowmoss.g / 255f, BardTheme.Glowmoss.b / 255f, 0.45f);
        p.strokeColor = BardTheme.Glowmoss;
        p.lineWidth = 2f;
        TracePolygon(p, center, radius, _levels);
        p.Fill();
        p.Stroke();
    }

    private static void TracePolygon(Painter2D p, Vector2 center, float radius, float[]? scale)
    {
        p.BeginPath();
        for (int i = 0; i < 5; i++)
        {
            float ang = (-Mathf.PI / 2f) + (i * 2f * Mathf.PI / 5f); // start at top, clockwise
            float r = radius * (scale == null ? 1f : Mathf.Max(0.04f, scale[i]));
            var pt = new Vector2(center.x + (Mathf.Cos(ang) * r), center.y + (Mathf.Sin(ang) * r));
            if (i == 0)
            {
                p.MoveTo(pt);
            }
            else
            {
                p.LineTo(pt);
            }
        }

        p.ClosePath();
    }
}
