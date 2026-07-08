namespace BardQuest.Domain.Ratings;

/// <summary>
/// Song intensity = the hardness tier (YARG's per-instrument star, taken as the source reports it —
/// 1..N, where custom songs can exceed 5; it is NOT normalized across games) plus a within-tier
/// sub-score in [0,1), computed from chart analysis, used only to order songs that share a tier.
/// The sub-score NEVER moves a song across tiers: SortKey(tier, sub) = tier + sub, and sub &lt; 1.
/// See <see cref="ChartRating"/> for how the star and the per-difficulty
/// derivation fit together.
/// </summary>
public static class ChartRatingCalculator
{
    /// <summary>Drum intensity tier. Uses YARG's intrinsic intensity when present (>=1, preserved
    /// as-is so custom songs can exceed 5), otherwise derives a tier from note density + BPM (1..6).</summary>
    public static int Tier(int rawIntensity, double notesPerSecond, int bpm)
    {
        if (rawIntensity >= 1)
        {
            return rawIntensity;
        }

        // Fallback for missing intensity (-1 / 0): map density+bpm into 1..6.
        double d = Math.Clamp(notesPerSecond / 14.0, 0, 1); // ~14 nps = very dense
        double s = Math.Clamp((bpm - 60) / 180.0, 0, 1); // 60..240 bpm
        double mix = (0.7 * d) + (0.3 * s); // density-weighted
        return Math.Clamp((int)Math.Round(1 + (mix * 5)), 1, 6);
    }

    // --- Profile-based sub-score (Phase 1: nuanced per-chart difficulty). Constants tunable here. ---
    //
    // Architecture: SubScore is absolute (not re-normalized within a tier) so that SortKey = Tier +
    // SubScore forms a smooth global difficulty curve. Density (peak-dominant) accounts for 70% of
    // the score because sheer note-rate is the primary determinant of chart difficulty; technique
    // scores act as a secondary differentiator for charts that would otherwise tie on density alone.

    public const double NpsCeil = 18.0; // notes/sec that normalizes density to 1.0; anything above this scores 1.0

    public const double
        DensityPeakWeight = 0.70; // peak-dominant density blend: DensityPeakWeight + DensityAvgWeight must equal 1.0

    public const double DensityAvgWeight = 0.30; // sustained average density gets less weight than the hard peaks

    public const double
        TechDoubleBass =
            0.30; // within-technique weights: TechDoubleBass + TechBlast + TechIndependence + TechFastFill must equal 1.0

    public const double
        TechBlast = 0.30; // blast beats are equally penalising as double bass (both demand extreme speed)

    public const double
        TechIndependence = 0.25; // limb independence is hard but less universally limiting than raw speed

    public const double TechFastFill = 0.15; // fast fills are demanding but brief, so they count least
    public const double BlendDensity = 0.70; // density >> technique: BlendDensity + BlendTechnique must equal 1.0
    public const double BlendTechnique = 0.30; // technique lifts a dense chart higher but cannot overcome density alone

    /// <summary>Within-tier ordering value in [0,1) from a chart's difficulty profile. Density (a
    /// peak-dominant blend) leads; the four graded techniques are a secondary modifier. Absolute, not
    /// re-normalized within a tier, so Tier + SubScore is a smooth global difficulty curve; the 0.999
    /// clamp guarantees it never crosses into the next tier.</summary>
    public static double SubScore(ChartDifficultyProfile p)
    {
        static double Norm(double nps)
        {
            return Math.Clamp(nps / NpsCeil, 0, 1);
        }

        double density = (DensityPeakWeight * Norm(p.PeakNps)) + (DensityAvgWeight * Norm(p.AvgNps));
        double technique = Math.Clamp(
            (TechDoubleBass * p.DoubleBass) + (TechBlast * p.BlastBeat) +
            (TechIndependence * p.Independence) + (TechFastFill * p.FastFill), 0, 1);
        double raw = (BlendDensity * density) + (BlendTechnique * technique);
        return Math.Clamp(raw, 0, 0.999);
    }

    /// <summary>Single sortable key. Tier dominates; sub-score only orders within a tier.</summary>
    public static double SortKey(int tier, double subScore) => tier + Math.Clamp(subScore, 0, 0.999);
}
