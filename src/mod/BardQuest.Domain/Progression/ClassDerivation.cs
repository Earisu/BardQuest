namespace BardQuest.Domain.Progression;

/// <summary>Buckets a player's five attribute levels (summed, 0–50 — the SAME axis a chart's rank
/// scores on, see <see cref="Ratings.AttributeProfile.ToRank"/>) into a (class, subrank). A sibling of
/// that chart-rank banding, not a call into it: shared
/// axis, own 18-band prestige-weighted banding (tighter toward the top so Legendweaver stays rare),
/// none of the chart-only boss/variety logic. Band edges are CALIBRATION TARGETS.</summary>
public static class ClassDerivation
{
    public const int SubranksPerClass = 3;
    public const double MaxScore = 50.0; // five levels × 10

    // Ascending upper bounds on the 0–50 score; a score below entry i is class i. Tighter toward the
    // top (prestige-weighted). Legendweaver is everything at/above the last bound.
    private static readonly (double Max, PlayerClass Class)[] Bands =
    [
        (12, PlayerClass.Busker),
        (21, PlayerClass.Minstrel),
        (29, PlayerClass.Troubadour),
        (36, PlayerClass.Bard),
        (43, PlayerClass.Skald),
    ];

    public static (PlayerClass Class, int Subrank) Derive(double score)
    {
        score = Math.Clamp(score, 0, MaxScore);
        (double lo, double hi, PlayerClass cls) = BandFor(score);
        double frac = hi > lo ? (score - lo) / (hi - lo) : 0;
        int subrank = Math.Clamp((int)(frac * SubranksPerClass), 0, SubranksPerClass - 1);
        return (cls, subrank);
    }

    private static (double Lo, double Hi, PlayerClass Class) BandFor(double score)
    {
        double lo = 0;
        foreach ((double max, PlayerClass cls) in Bands)
        {
            if (score < max)
            {
                return (lo, max, cls);
            }

            lo = max;
        }

        return (lo, MaxScore, PlayerClass.Legendweaver);
    }
}
