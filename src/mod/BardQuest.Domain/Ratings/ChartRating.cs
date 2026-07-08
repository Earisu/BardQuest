using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>
/// Rating of one chart — a single (instrument, charted difficulty) rendering of a song. A song owns
/// many charts; this is the unit BardQuest rates. <see cref="Tier"/> is YARG's per-instrument star at
/// this difficulty (taken as-is; customs may exceed 5); <see cref="SubScore"/> orders charts within a
/// tier from the density/technique profile. The song's hash is the cache key, not a field here.
/// </summary>
public sealed record ChartRating(
    Instrument Instrument,
    Difficulty Difficulty,
    int Tier,
    double SubScore,
    double RepresentativeNps)
{
    /// <summary>Single sortable difficulty value: tier dominates, sub-score orders within a tier.</summary>
    public double SortKey => ChartRatingCalculator.SortKey(Tier, SubScore);
}
