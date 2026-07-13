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

    private static Domain.Quest.Quest QuestWith(DeliveryState delivery, params ProvenanceLink[] links)
        => new(Guid.NewGuid(), Guid.NewGuid(), Instrument.ProDrums, Difficulty.Expert,
               QuestPace.Sprint, new DateTime(2026, 7, 11), links, delivery);

    [Fact]
    public void ResolvesLinksInDateOrderAndReportsClass()
    {
        RatedLibrary lib = Library(("a", 6), ("b", 6), ("c", 6));
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts>
        {
            [1] = Cleared(),
            [2] = Cleared(),
            [3] = Cleared(),
        });
        // Links deliberately out of date order — the engine must sort them.
        Domain.Quest.Quest quest = QuestWith(
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
        Domain.Quest.Quest quest = QuestWith(new DeliveryState(0, ["a"], null), new ProvenanceLink(1, "a", DateTime.UtcNow));

        ActiveQuestView view = QuestEngine.Resolve(quest, lib, scores);

        Assert.Equal(0.0, view.Profile.Score); // no plays folded
    }

    [Fact]
    public void WorkingSetMonstersReportDefeatedFromLinks()
    {
        RatedLibrary lib = Library(("a", 5), ("b", 6));
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts> { [1] = Cleared(0.95) });
        Domain.Quest.Quest quest = QuestWith(new DeliveryState(0, ["a", "b"], null), new ProvenanceLink(1, "a", DateTime.UtcNow));

        ActiveQuestView view = QuestEngine.Resolve(quest, lib, scores);

        MonsterStatus a = view.WorkingSet.Single(m => m.Hash == "a");
        MonsterStatus b = view.WorkingSet.Single(m => m.Hash == "b");
        Assert.True(a.Defeated);
        Assert.False(b.Defeated);
        // While grinding (one sub-breakthrough clear, not yet pressing a gate) the Elite is gated:
        // every delivered monster is Regular.
        Assert.False(view.AtMiniBoss);
        Assert.All(view.WorkingSet, m => Assert.Equal(MonsterType.Regular, m.Type));
    }

    [Fact]
    public void PlayedMonsterResolvesAndClearsDespiteHashCaseMismatch()
    {
        // The real-world casing split: the rated library + quest delivery key hashes UPPERCASE, while the
        // scores.db provenance link carries the SAME hash lowercase. The engine must still fold the play
        // (XP > 0) and mark that working-set monster defeated. A case-sensitive lookup silently fails both.
        RatedLibrary lib = Library(("ABCDEF", 6), ("B7612F", 6));
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts> { [1] = Cleared(0.95) });
        Domain.Quest.Quest quest = QuestWith(
            new DeliveryState(0, ["ABCDEF", "B7612F"], null),
            new ProvenanceLink(1, "abcdef", DateTime.UtcNow)); // lowercase link vs UPPERCASE delivery

        ActiveQuestView view = QuestEngine.Resolve(quest, lib, scores);

        Assert.True(view.Profile.Score > 0, "the lowercase link must resolve and fold into XP");
        MonsterStatus played = view.WorkingSet.Single(m => string.Equals(m.Hash, "ABCDEF", StringComparison.OrdinalIgnoreCase));
        Assert.True(played.Defeated, "the played monster must read as cleared regardless of hash case");
    }

    [Fact]
    public void EliteAppearsAloneOnlyDuringMiniBossBreakthrough()
    {
        // Four hard (sum 13, at/above the Minstrel floor) full clears climb Score past Busker I WITHOUT
        // ever clearing a same-rank Busker monster — so mini-boss gate 0 stays locked while honest XP
        // presses it. That is the breakthrough: the working set must collapse to the single Elite.
        RatedLibrary lib = Library(("hard", 13), ("m1", 6), ("m2", 10));
        var scores = new FakeScores(new Dictionary<int, PerformanceFacts>
        {
            [1] = Cleared(),
            [2] = Cleared(),
            [3] = Cleared(),
            [4] = Cleared(),
        });
        Domain.Quest.Quest quest = QuestWith(
            new DeliveryState(0, ["m1", "m2"], null),
            new ProvenanceLink(1, "hard", new DateTime(2026, 7, 1)),
            new ProvenanceLink(2, "hard", new DateTime(2026, 7, 2)),
            new ProvenanceLink(3, "hard", new DateTime(2026, 7, 3)),
            new ProvenanceLink(4, "hard", new DateTime(2026, 7, 4)));

        ActiveQuestView view = QuestEngine.Resolve(quest, lib, scores);

        Assert.True(view.AtMiniBoss);
        Assert.False(view.AtClassBoss);
        MonsterStatus elite = Assert.Single(view.WorkingSet);
        Assert.Equal(MonsterType.Elite, elite.Type);
        Assert.Equal("m2", elite.Hash); // the hardest same-rank monster is the Elite
    }
}
