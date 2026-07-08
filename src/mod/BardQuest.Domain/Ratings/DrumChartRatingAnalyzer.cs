using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>Rates a four-lane (pro) drums chart. Maps the neutral lane ints to <see cref="DrumPad"/>,
/// builds the density/technique profile, and produces a <see cref="ChartRating"/>.</summary>
public sealed class DrumChartRatingAnalyzer : IChartRatingAnalyzer
{
    public Instrument Instrument => Instrument.ProDrums;

    public ChartRating Analyze(
        IReadOnlyList<(double Time, int Lane)> hits,
        double durationSeconds,
        int rawIntensity,
        int bpm,
        Difficulty difficulty)
    {
        // hits arrive sorted ascending by time from the extractor; lanes are FourLaneDrumPad ordinals.
        var padHits = new List<(double Time, DrumPad Pad)>(hits.Count);
        foreach ((double time, int lane) in hits)
        {
            padHits.Add((time, (DrumPad)lane));
        }

        ChartDifficultyProfile profile = ChartAnalyzer.ProfileSorted(padHits, durationSeconds);
        int tier = ChartRatingCalculator.Tier(rawIntensity, profile.PeakNps, bpm);
        double subScore = ChartRatingCalculator.SubScore(profile);
        return new ChartRating(Instrument, difficulty, tier, subScore, profile.PeakNps);
    }
}
