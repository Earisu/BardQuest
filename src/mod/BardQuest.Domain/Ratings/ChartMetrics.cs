using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>What BardQuest persists per chart — a single (instrument, difficulty) rendering — its
/// YARG intensity star, and the family's typed raw measurements. Attribute scores and rank are
/// derived from <see cref="Raw"/> on load, never stored. <see cref="Intensity"/> &lt; 0 is the
/// negative-cache sentinel: the metadata claims the instrument but no rateable chart loaded, so the
/// scan won't re-parse it every refresh. Consumers MUST filter out sentinels.</summary>
public sealed record ChartMetrics(
    Instrument Instrument,
    Difficulty Difficulty,
    int Intensity,
    IRawMetrics Raw)
{
    public static ChartMetrics Sentinel(Instrument instrument)
        => new(instrument, Difficulty.Expert, -1, Drums.DrumRawMetrics.Zero);
}
