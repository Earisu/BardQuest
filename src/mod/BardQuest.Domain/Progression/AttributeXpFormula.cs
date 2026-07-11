using BardQuest.Domain.Ratings;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Domain.Progression;

/// <summary>Per-axis XP granted by one completed song: demand × quality × gapPenalty, scaled by
/// <see cref="LevelCurve.XpScale"/>. Quality rewards playing above the clear bar and makes No Fail a
/// non-issue without detecting it (weak clears earn ~nothing), using only scores.db fields. GapPenalty
/// gives exponential diminishing returns as you outgrow a song, so every clear can award without
/// farming. All constants are first-pass CALIBRATION TARGETS.</summary>
public static class AttributeXpFormula
{
    // Quality: percent ramps from ClearThreshold (a real clear) up to 1.0 at 100%; below it, ~0
    // (No-Fail junk). Full combo multiplies quality by (1 + FcKicker).
    public const double ClearThreshold = 0.85;
    public const double FcKicker = 0.15;

    // Gap: over = levels you've outgrown the song on this axis. Exponential decay, zero at Cutoff —
    // a bounded headroom above your library's hardest chart.
    public const double GapDecay = 0.7;
    public const int GapCutoff = 3;

    private static readonly Attribute[] Axes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    public static Dictionary<Attribute, double> Award(
        AttributeProfile songProfile, PerformanceFacts perf, IReadOnlyDictionary<Attribute, int> currentLevels)
    {
        double quality = QualityScale(perf);
        var award = new Dictionary<Attribute, double>(Axes.Length);
        foreach (Attribute axis in Axes)
        {
            double demand = songProfile[axis];
            int level = currentLevels.TryGetValue(axis, out int v) ? v : 0;
            award[axis] = LevelCurve.XpScale * demand * quality * GapPenalty(level - demand);
        }

        return award;
    }

    /// <summary>0 below the clear threshold, ramping to 1.0 at 100%, times the full-combo bonus.</summary>
    public static double QualityScale(PerformanceFacts perf)
    {
        double fromPercent = perf.Percent < ClearThreshold
            ? 0.0
            : (perf.Percent - ClearThreshold) / (1.0 - ClearThreshold);
        double fc = perf.IsFc ? 1.0 + FcKicker : 1.0;
        return Math.Clamp(fromPercent, 0, 1) * fc;
    }

    /// <summary>1.0 when the song is at/above your level, decaying to 0 by <see cref="GapCutoff"/>
    /// levels outgrown.</summary>
    public static double GapPenalty(double over)
    {
        if (over <= 0)
        {
            return 1.0;
        }

        return over >= GapCutoff ? 0.0 : Math.Pow(GapDecay, over);
    }
}
