using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>Instrument-family measurement seam. Each analyzer measures one instrument's chart into a
/// <see cref="ChartMetrics"/> (identity + typed raw). <paramref name="notes"/> is neutral
/// (time-seconds, lane-int, sync-tick) — the Mod's per-family extractor decides what "lane" means;
/// the analyzer's kit map turns lanes into roles. Adding a family = adding an analyzer + extractor.</summary>
public interface IChartAnalyzer
{
    Instrument Instrument { get; }

    ChartMetrics Analyze(
        IReadOnlyList<(double Time, int Lane, uint Tick)> notes,
        double durationSeconds,
        int intensity,
        Difficulty difficulty,
        SyncInfo sync);
}
