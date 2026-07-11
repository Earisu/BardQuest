using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Quest;

public class QuestProgressionTests
{
    private sealed class StubRaw(double sum) : IRawMetrics
    {
        public AttributeProfile ToAttributeProfile()
            => new(new Dictionary<Attribute, double> { [Attribute.Strength] = sum });
    }

    private sealed class FakeScores(Dictionary<int, PerformanceFacts> byId) : IScoreSource
    {
        public PerformanceFacts? Resolve(ProvenanceLink link)
            => byId.TryGetValue(link.PlayerScoreRecordId, out PerformanceFacts? f) ? f : null;
    }

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
    public void CreateStartsAnEmptyBuskerQuestWithADeliveredWorkingSet()
    {
        RatedLibrary lib = Library(("a", 3), ("b", 4), ("c", 5), ("d", 6), ("e", 7), ("f", 8));
        BardQuest.Domain.Quest.Quest quest = QuestFactory.Create(
            Guid.NewGuid(), Instrument.ProDrums, Difficulty.Expert, QuestPace.Journey, lib, new DateTime(2026, 7, 11));

        Assert.Empty(quest.Links);
        Assert.Equal(0, quest.Delivery.RerunCount);
        Assert.NotEmpty(quest.Delivery.WorkingSet);
        Assert.True(quest.Delivery.WorkingSet.Count <= MonsterMatcher.WorkingSetSize);
    }

    [Fact]
    public void RecordAppendsTheLink()
    {
        RatedLibrary lib = Library(("a", 5), ("b", 6), ("c", 7));
        BardQuest.Domain.Quest.Quest quest = QuestFactory.Create(
            Guid.NewGuid(), Instrument.ProDrums, Difficulty.Expert, QuestPace.Journey, lib, DateTime.UtcNow);
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts>
        {
            [1] = new(1.0, false, 5, 100, 0, Difficulty.Expert),
        });

        BardQuest.Domain.Quest.Quest after = QuestProgression.Record(quest, new ProvenanceLink(1, "a", DateTime.UtcNow), lib, scores);

        Assert.Single(after.Links);
        Assert.Equal(1, after.Links[0].PlayerScoreRecordId);
    }

    [Fact]
    public void RerunCounterIncrementsWhenTheBandPoolIsExhausted()
    {
        // A tiny library: two Busker-band monsters. After both are in the exclude set, a re-delivery
        // must bump RerunCount and re-offer rather than deliver an empty set.
        RatedLibrary lib = Library(("a", 5), ("b", 6));
        var quest = new BardQuest.Domain.Quest.Quest(
            Guid.NewGuid(), Guid.NewGuid(), Instrument.ProDrums, Difficulty.Expert, QuestPace.Sprint,
            DateTime.UtcNow, [], new DeliveryState(0, ["a", "b"], null));
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts>
        {
            [1] = new(1.0, false, 5, 100, 0, Difficulty.Expert),
            [2] = new(1.0, false, 5, 100, 0, Difficulty.Expert),
        });

        // Clear both delivered monsters; the second clear should trigger a rerun re-delivery.
        BardQuest.Domain.Quest.Quest afterA = QuestProgression.Record(quest, new ProvenanceLink(1, "a", DateTime.UtcNow), lib, scores);
        BardQuest.Domain.Quest.Quest afterB = QuestProgression.Record(afterA, new ProvenanceLink(2, "b", DateTime.UtcNow.AddMinutes(1)), lib, scores);

        Assert.True(afterB.Delivery.RerunCount >= 1);
        Assert.NotEmpty(afterB.Delivery.WorkingSet);
    }
}
