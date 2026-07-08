using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>
/// Instrument-agnostic rating seam. Each analyzer rates one instrument's chart from that chart's note
/// hits. <paramref name="hits"/> is a neutral (time-seconds, lane-int) list — the Mod's per-instrument
/// extractor decides what "lane" means (for drums it is the <see cref="DrumPad"/> ordinal). Adding an
/// instrument = adding an analyzer + extractor; nothing else changes.
/// </summary>
public interface IChartRatingAnalyzer
{
    Instrument Instrument { get; }

    ChartRating Analyze(
        IReadOnlyList<(double Time, int Lane)> hits,
        double durationSeconds,
        int rawIntensity,
        int bpm,
        Difficulty difficulty);
}
