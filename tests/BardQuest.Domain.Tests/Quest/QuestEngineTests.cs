// tests/BardQuest.Domain.Tests/Quest/QuestEngineTests.cs
using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Quest;

public class QuestEngineTests
{
    private sealed class StubRaw(double sum) : IRawMetrics
    {
        public AttributeProfile ToAttributeProfile()
            => new(new Dictionary<Attribute, double> { [Attribute.Strength] = sum });
    }

    // A score source backed by an in-memory rowid → facts table (the test's stand-in for scores.db).
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

    private static PerformanceFacts Cleared(double percent = 1.0)
        => new(percent, IsFc: false, Stars: 5, NotesHit: 100, NotesMissed: 0, Difficulty.Expert);

    private static BardQuest.Domain.Quest.Quest QuestWith(DeliveryState delivery, params ProvenanceLink[] links)
        => new(Guid.NewGuid(), Guid.NewGuid(), Instrument.ProDrums, Difficulty.Expert,
               QuestPace.Sprint, new DateTime(2026, 7, 11), links, delivery);

    [Fact]
    public void ResolvesLinksInDateOrderAndReportsClass()
    {
        RatedLibrary lib = Library(("a", 6), ("b", 6), ("c", 6));
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts>
        {
            [1] = Cleared(), [2] = Cleared(), [3] = Cleared(),
        });
        // Links deliberately out of date order — the engine must sort them.
        BardQuest.Domain.Quest.Quest quest = QuestWith(
            new DeliveryState(0, ["a", "b", "c"], null),
            new ProvenanceLink(3, "c", new DateTime(2026, 7, 3)),
            new ProvenanceLink(1, "a", new DateTime(2026, 7, 1)),
            new ProvenanceLink(2, "b", new DateTime(2026, 7, 2)));

        ActiveQuestView view = QuestEngine.Resolve(quest, lib, scores);

        Assert.Equal(PlayerClass.Busker, view.Class);
        Assert.Equal(3, view.WorkingSet.Count);
        Assert.True(view.Profile.Score > 0);
    }

    [Fact]
    public void UnresolvableLinksAreSkipped()
    {
        RatedLibrary lib = Library(("a", 6));
        var scores = new FakeScores([]); // resolves nothing
        BardQuest.Domain.Quest.Quest quest = QuestWith(new DeliveryState(0, ["a"], null), new ProvenanceLink(1, "a", DateTime.UtcNow));

        ActiveQuestView view = QuestEngine.Resolve(quest, lib, scores);

        Assert.Equal(0.0, view.Profile.Score); // no plays folded
    }

    [Fact]
    public void WorkingSetMonstersReportDefeatedFromLinks()
    {
        RatedLibrary lib = Library(("a", 5), ("b", 6));
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts> { [1] = Cleared(0.95) });
        BardQuest.Domain.Quest.Quest quest = QuestWith(new DeliveryState(0, ["a", "b"], null), new ProvenanceLink(1, "a", DateTime.UtcNow));

        ActiveQuestView view = QuestEngine.Resolve(quest, lib, scores);

        MonsterStatus a = view.WorkingSet.Single(m => m.Hash == "a");
        MonsterStatus b = view.WorkingSet.Single(m => m.Hash == "b");
        Assert.True(a.Defeated);
        Assert.False(b.Defeated);
        // Hardest in-set monster is the Elite mini-boss; the other is Regular
        Assert.Equal(BardQuest.Domain.Quest.MonsterType.Elite, view.WorkingSet.OrderByDescending(m => m.Sum).First().Type);
        Assert.Equal(BardQuest.Domain.Quest.MonsterType.Regular, view.WorkingSet.OrderBy(m => m.Sum).First().Type);
    }
}
