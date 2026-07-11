namespace BardQuest.Domain.Ratings;

/// <summary>A chart's five attribute scores (each in [0,10]). Descriptive — never summed into a
/// difficulty by consumers; its overall <see cref="Rank"/> is available via <see cref="ToRank"/>.
/// A missing attribute reads 0. All rank thresholds here are first-pass CALIBRATION TARGETS.</summary>
public sealed class AttributeProfile(IReadOnlyDictionary<Attribute, double> scores)
{
    // A varied chart is a touch harder than one looping a single bar — but a hard pattern stays hard
    // when repeated (Everlong loops one brutal bar), so variety can only ever ADD, never sink. Zero
    // below the library-median variety, so the looping half of the library is untouched, and capped
    // well under one band width so it nudges edge cases rather than reshuffling the ladder. (3.33 = the
    // old 4.0 rescaled by 5/6 when the score moved from a 0..60 mean to a 0..50 sum.)
    public const double VarietyBonusMax = 3.33;
    public const double VarietyFloor = 0.35, VarietyCeil = 0.80;

    public const double BossAttributeThreshold = 9.0; // a single axis at/above this = "boss"

    // Ascending upper bounds on the 0..50 rank score; a score below entry i earns rank i. These are the
    // prior 0..60 edges rescaled by 5/6 (the score is the raw five-axis sum, not a mean × 6) and rounded
    // to integers — a behaviour-preserving move that leaves the library's rank distribution all but
    // identical (SSS still means a five-axis sum of ~47, i.e. a mean ~9.4).
    private static readonly (double Max, Rank Rank)[] Bands =
    [
        (11, Rank.F), (18, Rank.E), (24, Rank.D), (29, Rank.C), (34, Rank.B),
        (38, Rank.A), (43, Rank.S), (47, Rank.SS),
    ];

    public IReadOnlyDictionary<Attribute, double> Scores { get; } = scores;

    public double this[Attribute a] => Scores.TryGetValue(a, out double v) ? v : 0.0;

    /// <summary>Unweighted sum of all five axes (0..50).</summary>
    public double Sum()
    {
        double total = 0;
        foreach (Attribute a in Enum.GetValues(typeof(Attribute)))
        {
            total += this[a];
        }

        return total;
    }

    /// <summary>Highest single attribute (0..10) — the chart's "threat level" for the monster sheet:
    /// a lopsided specialist and a well-rounded chart with the same <see cref="Sum"/> read differently.</summary>
    public double Threat()
    {
        double max = 0;
        foreach (Attribute a in Enum.GetValues(typeof(Attribute)))
        {
            max = Math.Max(max, this[a]);
        }

        return max;
    }

    /// <summary>Buckets this profile into a <see cref="Rank"/>: the five-axis <see cref="Sum"/> (0..50,
    /// all-tens = 50 = SSS) plus a gentle pattern-variety bonus, bucketed by prestige-weighted bands
    /// that tighten toward the top. A "boss" chart — one whose top axis is at/above
    /// <see cref="BossAttributeThreshold"/> while already ranking B or higher — is promoted one rank
    /// (up to SSS), capturing specialists whose overall breadth understates them.</summary>
    public Rank ToRank(double patternVariety = 0)
    {
        Rank rank = Band(Sum() + VarietyBonus(patternVariety));

        if (Threat() >= BossAttributeThreshold && rank >= Rank.B && rank < Rank.SSS)
        {
            rank += 1;
        }

        return rank;
    }

    private static double VarietyBonus(double patternVariety)
        => VarietyBonusMax * Math.Clamp((patternVariety - VarietyFloor) / (VarietyCeil - VarietyFloor), 0, 1);

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
