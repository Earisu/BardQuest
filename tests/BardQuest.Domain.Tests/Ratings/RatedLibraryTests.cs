using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Ratings;

public class RatedLibraryTests
{
    // A raw-metrics stub that derives a fixed profile, so the library can be built without a real analyzer.
    private sealed class StubRaw(double strength) : IRawMetrics
    {
        public AttributeProfile ToAttributeProfile()
            => new(new Dictionary<Attribute, double> { [Attribute.Strength] = strength });
    }

    private static ChartMetrics Chart(double strength, Difficulty diff = Difficulty.Expert, int intensity = 3)
        => new(Instrument.ProDrums, diff, intensity, new StubRaw(strength));

    private static Dictionary<string, IReadOnlyList<ChartMetrics>> Cache(
        params (string Hash, ChartMetrics[] Charts)[] songs)
    {
        var d = new Dictionary<string, IReadOnlyList<ChartMetrics>>();
        foreach ((string hash, ChartMetrics[] charts) in songs)
        {
            d[hash] = charts;
        }

        return d;
    }

    [Fact]
    public void ProfileResolvesTheChartForTheConfiguredInstrumentAndDifficulty()
    {
        var lib = new RatedLibrary(Cache(("h1", [Chart(strength: 7)])), Instrument.ProDrums, Difficulty.Expert);
        Assert.Equal(7.0, lib.Profile("h1")!.Sum());
        Assert.Null(lib.Profile("missing"));
    }

    [Fact]
    public void SongsAreOrderedBySumAscending()
    {
        var lib = new RatedLibrary(
            Cache(("hard", [Chart(strength: 9)]), ("easy", [Chart(strength: 2)])),
            Instrument.ProDrums, Difficulty.Expert);

        var songs = lib.Songs();
        Assert.Equal(["easy", "hard"], songs.Select(s => s.Hash));
        Assert.Equal(2.0, songs[0].Sum);
    }

    [Fact]
    public void SentinelsAndOtherDifficultiesAreExcluded()
    {
        var lib = new RatedLibrary(
            Cache(
                ("sentinel", [ChartMetrics.Sentinel(Instrument.ProDrums)]),
                ("wrongDiff", [Chart(strength: 5, diff: Difficulty.Hard)]),
                ("ok", [Chart(strength: 4)])),
            Instrument.ProDrums, Difficulty.Expert);

        Assert.Equal(["ok"], lib.Songs().Select(s => s.Hash));
    }
}
