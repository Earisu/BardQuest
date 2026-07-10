using BardQuest.Domain.Ratings;
using BardQuest.Domain.Ratings.Drums;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Ratings;

public class RatingCacheTests
{
    private static DrumRawMetrics Raw(double peak) =>
        new(5, peak, 3, 2, 12, 18, 12, 0.06, 0.4, 0.1, 0.6, 1.1, 0.8, 2.3, 0.5, 6.8, 1.4);

    private static byte[] Write(IReadOnlyDictionary<string, IReadOnlyList<ChartMetrics>> data)
    {
        using var ms = new MemoryStream();
        RatingCache.Serialize(data, ms);
        return ms.ToArray();
    }

    [Fact]
    public void RoundTrips_MetricsAndSentinel()
    {
        var data = new Dictionary<string, IReadOnlyList<ChartMetrics>>
        {
            ["hashA"] =
            [
                new(Instrument.ProDrums, Difficulty.Expert, 5, Raw(14.5)),
                ChartMetrics.Sentinel(Instrument.ProDrums),
            ],
        };

        using var ms = new MemoryStream(Write(data));
        Dictionary<string, List<ChartMetrics>>? back = RatingCache.Deserialize(ms);

        Assert.NotNull(back);
        List<ChartMetrics> charts = back["hashA"];
        Assert.Equal(2, charts.Count);
        var rated = (DrumRawMetrics)charts[0].Raw;
        Assert.Equal(14.5, rated.PeakNps, 6);
        Assert.Equal(Raw(14.5), rated); // every field survives — guards the codec's byte offsets (incl. the int at field 5)
        Assert.Equal(Instrument.ProDrums, charts[0].Instrument);
        Assert.Equal(Difficulty.Expert, charts[0].Difficulty);
        Assert.Equal(5, charts[0].Intensity);
        Assert.True(charts[1].Intensity < 0); // sentinel survived
    }

    [Fact]
    public void BadMagic_ReturnsNull()
    {
        using var ms = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);
        Assert.Null(RatingCache.Deserialize(ms));
    }

    [Fact]
    public void Truncated_ReturnsNull()
    {
        byte[] full = Write(new Dictionary<string, IReadOnlyList<ChartMetrics>>
        {
            ["h"] = [new(Instrument.ProDrums, Difficulty.Expert, 5, Raw(9))],
        });
        using var ms = new MemoryStream(full[..(full.Length / 2)]);
        Assert.Null(RatingCache.Deserialize(ms));
    }
}
