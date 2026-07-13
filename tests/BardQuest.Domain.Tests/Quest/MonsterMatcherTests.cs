using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Quest;

public class MonsterMatcherTests
{
    private sealed class StubRaw(double sum) : IRawMetrics
    {
        public AttributeProfile ToAttributeProfile()
            => new(new Dictionary<Attribute, double> { [Attribute.Strength] = sum });
    }

    // Builds a library whose songs have exactly the given Sum values (all one Strength axis).
    private static RatedLibrary Library(params (string Hash, double Sum)[] songs)
    {
        var d = new Dictionary<string, IReadOnlyList<ChartMetrics>>();
        foreach ((string hash, double sum) in songs)
        {
            d[hash] = [new ChartMetrics(Instrument.ProDrums, Difficulty.Expert, 3, new StubRaw(sum))];
        }

        return new RatedLibrary(d, Instrument.ProDrums, Difficulty.Expert);
    }

    [Fact]
    public void WindowCentersJustAbovePlayerScoreClampedToBand()
    {
        DeliveryWindow w = MonsterMatcher.Window(playerScore: 5, band: PlayerClass.Busker); // Busker = [0,12)
        Assert.True(w.Center > 5);
        Assert.True(w.Center <= 12);
        Assert.True(w.Lo >= 0 && w.Hi <= 12);
    }

    [Fact]
    public void WorkingSetPicksInBandMonstersNearestCenterExcludingGiven()
    {
        RatedLibrary lib = Library(("a", 4), ("b", 5), ("c", 6), ("d", 7), ("e", 8), ("far", 40));
        DeliveryWindow w = MonsterMatcher.Window(playerScore: 5, band: PlayerClass.Busker);

        IReadOnlyList<string> set = MonsterMatcher.WorkingSet(lib, w, size: 3, exclude: new HashSet<string> { "b" });

        Assert.Equal(3, set.Count);
        Assert.DoesNotContain("b", set);
        Assert.DoesNotContain("far", set); // way out of band
    }

    [Fact]
    public void PickBossReturnsLowestMonsterAtOrAboveNextClassFloor()
    {
        // Busker's next floor is 12 (Minstrel floor). Bosses must be >= 12; pick the lowest such.
        RatedLibrary lib = Library(("busker", 9), ("boss", 13), ("harder", 20));
        string? boss = MonsterMatcher.PickBoss(lib, PlayerClass.Busker, exclude: new HashSet<string>());
        Assert.Equal("boss", boss);
    }

    [Fact]
    public void PickBossIsNullWhenNothingReachesTheNextFloor()
    {
        RatedLibrary lib = Library(("busker", 9), ("stillBusker", 11));
        Assert.Null(MonsterMatcher.PickBoss(lib, PlayerClass.Busker, exclude: new HashSet<string>()));
    }
}
