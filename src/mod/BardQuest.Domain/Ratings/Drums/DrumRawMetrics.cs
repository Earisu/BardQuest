namespace BardQuest.Domain.Ratings.Drums;

/// <summary>The raw drum measurements cached per chart (the source of truth). Attribute scores and
/// rank are derived from these on load and never stored — so scoring formulas retune without a
/// rescan. Only the fields an active derivation reads are kept; adding or removing a field is a
/// cache-format change and forces a one-time rescan.</summary>
public sealed record DrumRawMetrics(
    double AvgNps,
    double PeakNps,
    double LongestDenseSectionSeconds,
    double KickDensity,
    int LongestKickRun,
    double PeakBurstNps,
    double FastFillRate,
    double ShortestTransitionGap,
    double SyncopationFraction,
    double OddMeterFraction,
    double PatternVariety,
    double OffCarrierPerSec,
    double OffCarrierFastPerSec,
    double ResidualAltPerSec,
    double NoCarrierAltPerSec,
    double FastestKickSpanNps,
    double KitPieceEntropy) : IRawMetrics
{
    public static DrumRawMetrics Zero { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
