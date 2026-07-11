namespace BardQuest.Domain.Ratings.Drums;

/// <summary>The raw drum measurements cached per chart (the source of truth). Attribute scores and
/// rank are derived from these on load and never stored — so scoring formulas retune without a
/// rescan. Only the fields an active derivation reads are kept; adding or removing a field is a
/// cache-format change and forces a one-time rescan. <see cref="ToAttributeProfile"/> is the drum
/// family's derivation of the five shared attribute axes; every constant it uses is a first-pass
/// CALIBRATION TARGET, tuned by playtest without rescanning.</summary>
public sealed record DrumRawMetrics(
    double AvgNps,
    double PeakNps,
    double LongestDenseSectionSeconds,
    double KickDensity,
    int LongestKickRun,
    double PeakBurstNps,
    double FastFillRate,
    double ShortestTransitionGap,
    double PatternVariety,
    double OffCarrierPerSec,
    double OffCarrierFastPerSec,
    double ResidualAltPerSec,
    double NoCarrierAltPerSec,
    double FastestKickSpanNps,
    double KitPieceEntropy) : IRawMetrics
{
    public static DrumRawMetrics Zero { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    // Reference ceilings that map a raw value to a 10 — each set to roughly the p97–p99 of the real
    // library so a chart that genuinely maxes a skill scores ~10 on it (an all-tens chart = rank SSS).
    public const double AvgNpsCeil = 11, PeakNpsCeil = 16, DenseCeil = 30, KickDensityCeil = 3.5;
    public const double KickRunCeil = 28, BurstCeil = 20, FastFillCeil = 16;
    public const double FastGapFloor = 0.20; // gaps at/below ~this map Agility's speed term toward 10

    // Endurance's burst term is the window-free fastest 8-kick span; the library's fastest real feet
    // sit at ~7.7 kicks/s, so ~7 is the p97-ish ceiling (the old windowed metric quantised to {2,4,6,8}).
    public const double FastestKickSpanCeil = 7.0;

    // Technique = limb independence, as carrier-stripped events/sec (see MeasureIndependence): what
    // the figure limbs genuinely do against the timekeeping ostinato. Rates are speed-weighted by
    // construction — a slow jazzy weave reads low with no extra gate — and the carrier-stripping is
    // what tells "fast and independent" (Everlong, blasts) from "fast but simple" (a driven backbeat).
    // Off-carrier work under a SLOW ostinato (funk ghost figures, shuffles) is real but easier, so it
    // enters at reduced weight. The sqrt lifts the mid-range: the raw event rates span ~40x between
    // easy and brutal charts, far wider than the perceived difficulty gap.
    public const double IndependenceCeil = 2.0;   // events/sec at which Technique saturates
    public const double OffCarrierSlowWeight = 0.6;

    // Dexterity = kit-ranging: breadth of coverage GATED BY non-repetition. KitPieceEntropy (bits across
    // distinct pieces) says "how many pieces", but a repetitive multi-piece groove touches many pieces
    // without ranging — a White-Stripes floor-tom loop reads mid on entropy alone. PatternVariety
    // (distinct bars / total) says "how much it actually changes", so the product scores high only when
    // the chart uses many pieces AND keeps varying: the honest "moving around the kit" demand. The gate
    // saturates at DexVarietySat; the ceiling maps the broad-and-varied end to 10. Both raw inputs are
    // cached, so this axis retunes without a rescan. (Replaces tom+cymbal FRACTION, which over-credited
    // single-piece pounding.)
    public const double KitBreadthCeil = 2.25;
    public const double DexVarietySat = 0.45; // PatternVariety at which the non-repetition gate is full

    /// <summary>Derives the five 0–10 attribute scores from these raw drum measurements. Runs on load,
    /// never cached — guitar/bass get their own derivation of the same attributes.</summary>
    public AttributeProfile ToAttributeProfile()
    {
        double strength = 10 * Avg(Norm(PeakNps, PeakNpsCeil), Norm(AvgNps, AvgNpsCeil), Norm(LongestDenseSectionSeconds, DenseCeil));
        double endurance = 10 * Avg(Norm(KickDensity, KickDensityCeil), Norm(LongestKickRun, KickRunCeil), Norm(FastestKickSpanNps, FastestKickSpanCeil));
        double independenceEvents = ResidualAltPerSec + NoCarrierAltPerSec + OffCarrierFastPerSec
            + (OffCarrierSlowWeight * (OffCarrierPerSec - OffCarrierFastPerSec));
        double technique = 10 * Math.Sqrt(Norm(independenceEvents, IndependenceCeil));
        double agility = 10 * Avg(Norm(PeakBurstNps, BurstCeil), Norm(FastFillRate, FastFillCeil), InvGap(ShortestTransitionGap));
        double kitRanging = KitPieceEntropy * Math.Min(PatternVariety / DexVarietySat, 1.0);
        double dexterity = 10 * Norm(kitRanging, KitBreadthCeil);

        return new AttributeProfile(new Dictionary<Attribute, double>
        {
            [Attribute.Strength] = strength,
            [Attribute.Endurance] = endurance,
            [Attribute.Technique] = technique,
            [Attribute.Agility] = agility,
            [Attribute.Dexterity] = dexterity,
        });
    }

    private static double Norm(double v, double ceil) => Math.Clamp(v / ceil, 0, 1);

    private static double InvGap(double gap)
        => gap <= 0 ? 0.0 : Math.Clamp((FastGapFloor - gap) / FastGapFloor, 0, 1);

    private static double Avg(params double[] xs)
    {
        double s = 0;
        foreach (double x in xs)
        {
            s += x;
        }

        return xs.Length == 0 ? 0 : s / xs.Length;
    }
}
