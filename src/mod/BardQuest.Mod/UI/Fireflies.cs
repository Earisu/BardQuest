using UnityEngine;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// A soft field of gold motes over the backdrop. Each mote fades in, brightens, and fades fully out on a
// staggered cycle; the instant it goes invisible it hops to a fresh spot (drawn from a scattered pool of
// anchors, plus a little jitter) so the field keeps "popping" in new places rather than pulsing in fixed
// positions. Purely decorative — the overlay and every mote ignore input.
public sealed class Fireflies
{
    private const int Count = 14;

    // Scattered anchor spots (percent of the surface) where motes may appear — spread across the forest,
    // kept off the dead centre where the logo/panels sit.
    private static readonly Vector2[] Spots =
    [
        new(6f, 30f), new(12f, 62f), new(18f, 20f), new(24f, 78f), new(31f, 46f),
        new(40f, 14f), new(46f, 71f), new(55f, 34f), new(62f, 82f), new(70f, 24f),
        new(76f, 58f), new(83f, 39f), new(88f, 72f), new(92f, 17f), new(50f, 88f),
        new(34f, 57f), new(66f, 51f),
    ];

    private readonly Mote[] _motes = new Mote[Count];
    private readonly System.Random _rng = new(1234);
    private readonly IVisualElementScheduledItem _tick;

    public VisualElement Root { get; }

    public Fireflies(BardQuestArt art)
    {
        Root = new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 } };
        for (int i = 0; i < Count; i++)
        {
            var img = new Image
            {
                image = art.Glow(),
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, width = 26, height = 26, opacity = 0f },
            };
            Root.Add(img);
            var mote = new Mote(img, period: 4.5f + ((float)_rng.NextDouble() * 2.1f), phase: (float)_rng.NextDouble());
            Place(mote);
            _motes[i] = mote;
        }

        _tick = Root.schedule.Execute(Tick).Every(16);
    }

    // Pause the per-frame tick while the surface is hidden — there is nothing to animate on an invisible
    // overlay, and it avoids a perpetual tick running through song gameplay.
    public void SetRunning(bool running)
    {
        if (running)
        {
            _tick.Resume();
        }
        else
        {
            _tick.Pause();
        }
    }

    private void Tick()
    {
        float t = Time.realtimeSinceStartup;
        foreach (Mote m in _motes)
        {
            float u = Mathf.Repeat((t / m.Period) + m.Phase, 1f);           // 0..1 sawtooth
            if (u < m.PrevU)
            {
                Place(m);                                                   // cycle wrapped while invisible -> new spot
            }

            m.PrevU = u;

            float k = 0.5f - (0.5f * Mathf.Cos(u * 2f * Mathf.PI));          // 0 at ends, 1 at mid
            m.Image.style.opacity = Mathf.Lerp(0f, 1f, k);
            m.Image.style.scale = new Scale(Vector2.one * Mathf.Lerp(0.55f, 1.3f, k));
            m.Image.style.translate = new Translate(Mathf.Lerp(0f, 12f, k), Mathf.Lerp(0f, -18f, k));
        }
    }

    private void Place(Mote m)
    {
        Vector2 spot = Spots[_rng.Next(Spots.Length)];
        float jx = ((float)_rng.NextDouble() - 0.5f) * 7f;
        float jy = ((float)_rng.NextDouble() - 0.5f) * 7f;
        m.Image.style.left = Length.Percent(Mathf.Clamp(spot.x + jx, 0f, 98f));
        m.Image.style.top = Length.Percent(Mathf.Clamp(spot.y + jy, 2f, 96f));
    }

    private sealed class Mote(Image image, float period, float phase)
    {
        public Image Image { get; } = image;
        public float Period { get; } = period;
        public float Phase { get; } = phase;
        public float PrevU { get; set; } = phase;
    }
}
