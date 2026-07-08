using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Ratings;

public class DrumChartRatingAnalyzerTests
{
    // A steady 8 notes/sec snare stream for ~5s.
    private static (double, int)[] SnareStream(int count, double rate)
    {
        var a = new (double, int)[count];
        for (int i = 0; i < count; i++)
        {
            a[i] = (i / rate, (int)DrumPad.RedDrum);
        }

        return a;
    }

    [Fact]
    public void Instrument_IsProDrums()
        => Assert.Equal(Instrument.ProDrums, new DrumChartRatingAnalyzer().Instrument);

    [Fact]
    public void Analyze_UsesRawIntensityAsTier_WhenPresent()
    {
        ChartRating r = new DrumChartRatingAnalyzer()
            .Analyze(SnareStream(40, 8.0), durationSeconds: 5.0, rawIntensity: 4, bpm: 120, Difficulty.Expert);
        Assert.Equal(4, r.Tier);
        Assert.Equal(Difficulty.Expert, r.Difficulty);
        Assert.Equal(Instrument.ProDrums, r.Instrument);
    }

    [Fact]
    public void Analyze_SubScoreInRange_AndSortKeyIsTierPlusSubScore()
    {
        ChartRating r = new DrumChartRatingAnalyzer()
            .Analyze(SnareStream(40, 8.0), durationSeconds: 5.0, rawIntensity: 3, bpm: 120, Difficulty.Hard);
        Assert.InRange(r.SubScore, 0.0, 0.999);
        Assert.Equal(r.Tier + r.SubScore, r.SortKey, 6);
    }

    [Fact]
    public void Analyze_RepresentativeNps_IsThePeakNps()
    {
        (double, int)[] hits = SnareStream(80, 8.0);
        ChartRating r = new DrumChartRatingAnalyzer()
            .Analyze(hits, durationSeconds: 10.0, rawIntensity: 5, bpm: 120, Difficulty.Expert);
        // PeakNps of a steady 8/s stream is ~8.
        Assert.True(Math.Abs(r.RepresentativeNps - 8.0) <= 1.0, $"peak nps {r.RepresentativeNps}");
    }

    // Instrument-agnostic seam: a second analyzer implementing the same interface produces a rating
    // tagged with its own instrument — proving the seam is not drum-coupled.
    private sealed class FakeGuitarAnalyzer : IChartRatingAnalyzer
    {
        public Instrument Instrument => Instrument.FiveFretGuitar;

        public ChartRating Analyze(IReadOnlyList<(double Time, int Lane)> hits,
            double durationSeconds, int rawIntensity, int bpm, Difficulty difficulty)
            => new(Instrument, difficulty, rawIntensity, 0.5, 0.0);
    }

    [Fact]
    public void Registry_EachAnalyzerProducesARatingForItsOwnInstrument()
    {
        IChartRatingAnalyzer[] registry = [new DrumChartRatingAnalyzer(), new FakeGuitarAnalyzer()];
        (double, int)[] hits = SnareStream(40, 8.0);

        ChartRating[] ratings =
            [.. registry.Select(a => a.Analyze(hits, 5.0, 4, 120, Difficulty.Expert))];

        Assert.Equal(Instrument.ProDrums, ratings[0].Instrument);
        Assert.Equal(Instrument.FiveFretGuitar, ratings[1].Instrument);
    }
}
