using BardQuest.Domain.Ratings;
using BardQuest.Domain.Ratings.Drums;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalyzerTests
{
    private static readonly SyncInfo Sync = new(480, [new(0.0, 4, 4)]);

    private static DrumChartAnalyzer Pro() => new(Instrument.ProDrums, DrumKitMap.ProFourLane);

    private static List<(double, int, uint)> Snare(int count, double rate)
        => [.. Enumerable.Range(0, count).Select(i => (i / rate, 1, (uint)(i * 240)))];

    [Fact]
    public void Instrument_IsWhatItWasConstructedWith()
        => Assert.Equal(Instrument.ProDrums, Pro().Instrument);

    [Fact]
    public void Analyze_ProducesChartMetrics_WithDrumRaw()
    {
        ChartMetrics m = Pro().Analyze(Snare(40, 8.0), 5.0, intensity: 4, Difficulty.Expert, Sync);
        Assert.Equal(Instrument.ProDrums, m.Instrument);
        Assert.Equal(Difficulty.Expert, m.Difficulty);
        Assert.Equal(4, m.Intensity);
        DrumRawMetrics raw = Assert.IsType<DrumRawMetrics>(m.Raw);
        Assert.True(raw.PeakNps > 0);
    }

    [Fact]
    public void Analyze_DropsOutOfVocabularyLanes()
    {
        var notes = new List<(double, int, uint)> { (0.0, 1, 0u), (0.1, 99, 0u), (0.2, 1, 240u) };
        ChartMetrics m = Pro().Analyze(notes, 1.0, 3, Difficulty.Hard, Sync);
        DrumRawMetrics raw = Assert.IsType<DrumRawMetrics>(m.Raw);
        Assert.True(raw.AvgNps > 0); // did not throw on the bad lane
    }

    [Fact]
    public void Sentinel_HasNegativeIntensity_AndZeroRaw()
    {
        var s = ChartMetrics.Sentinel(Instrument.ProDrums);
        Assert.True(s.Intensity < 0);
        Assert.Equal(DrumRawMetrics.Zero, s.Raw);
    }
}
