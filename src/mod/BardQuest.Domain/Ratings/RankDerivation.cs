namespace BardQuest.Domain.Ratings;

/// <summary>Buckets a chart into a <see cref="Rank"/> from the five attributes — Strength, Endurance,
/// Technique, Agility and Dexterity — the axes that track drum difficulty. The rank score is simply their
/// sum (0..50, so all-tens = 50 = SSS), plus a gentle pattern-variety bonus, bucketed by
/// prestige-weighted bands that tighten toward the top. A "boss" chart — one that maxes a single rank
/// axis (>= <see cref="BossAttributeThreshold"/>) while already ranking B or higher — is promoted one
/// rank (up to SSS), capturing specialists whose overall breadth understates them. Thresholds are
/// calibration targets.</summary>
public static class RankDerivation
{
    // The five axes that drive the rank. Dexterity earns its place: recalibrated to kit-ranging, it
    // separates easy from hard about as well as Agility.
    private static readonly Attribute[] RankAxes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    // A varied chart is a touch harder than one looping a single bar — but a hard pattern stays hard
    // when repeated (Everlong loops one brutal bar), so variety can only ever ADD, never sink. Zero
    // below the library-median variety, so the looping half of the library is untouched, and capped
    // well under one band width so it nudges edge cases rather than reshuffling the ladder. (3.33 = the
    // old 4.0 rescaled by 5/6 when the score moved from a 0..60 mean to a 0..50 sum.)
    public const double VarietyBonusMax = 3.33;
    public const double VarietyFloor = 0.35, VarietyCeil = 0.80;

    // Ascending upper bounds on the 0..50 rank score; a score below entry i earns rank i. These are the
    // prior 0..60 edges rescaled by 5/6 (the score is now the raw five-axis sum, not a mean × 6) and
    // rounded to integers — a behaviour-preserving move that leaves the library's rank distribution all
    // but identical (SSS still means a five-axis sum of ~47, i.e. a mean ~9.4).
    private static readonly (double Max, Rank Rank)[] Bands =
    [
        (11, Rank.F), (18, Rank.E), (24, Rank.D), (29, Rank.C), (34, Rank.B),
        (38, Rank.A), (43, Rank.S), (47, Rank.SS),
    ];

    public const double BossAttributeThreshold = 9.0; // a single rank axis at/above this = "boss"

    public static Rank Derive(AttributeProfile profile, double patternVariety = 0)
    {
        Rank rank = Band(RankScore(profile) + VarietyBonus(patternVariety));

        // Boss promotion: a chart that maxes one physical axis and already ranks B+ climbs one rank
        // (capped at SSS), so an infamous one-skill chart isn't held back by its overall breadth.
        double peak = 0;
        foreach (Attribute a in RankAxes)
        {
            peak = Math.Max(peak, profile[a]);
        }

        if (peak >= BossAttributeThreshold && rank >= Rank.B && rank < Rank.SSS)
        {
            rank += 1;
        }

        return rank;
    }

    private static double VarietyBonus(double patternVariety)
        => VarietyBonusMax * Math.Clamp((patternVariety - VarietyFloor) / (VarietyCeil - VarietyFloor), 0, 1);

    /// <summary>Sum of the five rank axes (0..50, all-tens = 50).</summary>
    private static double RankScore(AttributeProfile profile)
    {
        double sum = 0;
        foreach (Attribute a in RankAxes)
        {
            sum += profile[a];
        }

        return sum;
    }

    private static Rank Band(double score)
    {
        foreach ((double max, Rank rank) in Bands)
        {
            if (score < max)
            {
                return rank;
            }
        }

        return Rank.SSS;
    }
}
