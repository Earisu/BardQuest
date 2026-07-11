using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

namespace BardQuest.Domain.Quest;

/// <summary>The write path: append a provenance link, re-derive the standing, and refresh delivery — set
/// the exclusive boss when a class-boss phase is entered, redeliver the working set (with rerun re-offer)
/// when it is depleted. Returns a new <see cref="Quest"/> for the caller to persist. Pure.</summary>
public static class QuestProgression
{
    public static Quest Record(Quest quest, ProvenanceLink link, RatedLibrary library, IScoreSource scores)
    {
        var links = new List<ProvenanceLink>(quest.Links) { link };
        Quest updated = quest with { Links = links };

        QuestState state = Fold(updated, library, scores);
        return updated with { Delivery = Deliver(updated, state, library, scores) };
    }

    private static QuestState Fold(Quest quest, RatedLibrary library, IScoreSource scores)
    {
        var plays = new List<(AttributeProfile, PerformanceFacts, DateTime)>();
        foreach (ProvenanceLink l in quest.Links)
        {
            AttributeProfile? profile = library.Profile(l.SongHashHex);
            PerformanceFacts? facts = scores.Resolve(l);
            if (profile != null && facts != null)
            {
                plays.Add((profile, facts, l.PlayedAt));
            }
        }

        plays.Sort((x, y) => x.Item3.CompareTo(y.Item3));
        return QuestFold.Run(plays.Select(p => (p.Item1, p.Item2)).ToList(), quest.Pace);
    }

    private static DeliveryState Deliver(Quest quest, QuestState state, RatedLibrary library, IScoreSource scores)
    {
        PlayerClass band = QuestLadder.ClassOfStep(state.EffectiveStep);
        bool atBoss = state.GatesUnlocked < QuestLadder.TopStep
                   && QuestLadder.IsClassBossGate(state.GatesUnlocked)
                   && QuestLadder.StepForScore(state.Profile.Score) > state.GatesUnlocked;

        // Class-boss phase: deliver ONLY the boss (exclusive). Keep the existing boss if still valid.
        if (atBoss)
        {
            string? boss = quest.Delivery.BossHash ?? MonsterMatcher.PickBoss(library, band, new HashSet<string>());
            return quest.Delivery with { WorkingSet = boss == null ? [] : [boss], BossHash = boss };
        }

        // Climbing phase: keep the current set unless it's depleted (every monster defeated at the
        // band's mini-boss bar), then redeliver — with a rerun re-offer if the pool is exhausted.
        HashSet<string> defeated = DefeatedHashes(quest, library, scores, QuestLadder.MiniBossBar(band));
        bool depleted = quest.Delivery.WorkingSet.Count == 0 || quest.Delivery.WorkingSet.All(defeated.Contains);
        if (!depleted)
        {
            return quest.Delivery with { BossHash = null };
        }

        DeliveryWindow window = MonsterMatcher.Window(state.Profile.Score, band);
        IReadOnlyList<string> next = MonsterMatcher.WorkingSet(
            library, window, MonsterMatcher.WorkingSetSize, defeated);

        // Exhausted the band's un-defeated pool → bump the rerun counter and re-offer everything.
        if (next.Count == 0)
        {
            next = MonsterMatcher.WorkingSet(library, window, MonsterMatcher.WorkingSetSize, new HashSet<string>());
            return new DeliveryState(quest.Delivery.RerunCount + 1, next, BossHash: null);
        }

        return new DeliveryState(quest.Delivery.RerunCount, next, BossHash: null);
    }

    private static HashSet<string> DefeatedHashes(Quest quest, RatedLibrary library, IScoreSource scores, double bar)
    {
        var defeated = new HashSet<string>();
        foreach (ProvenanceLink l in quest.Links)
        {
            PerformanceFacts? facts = scores.Resolve(l);
            if (facts != null && facts.Percent + 1e-9 >= bar && library.Profile(l.SongHashHex) != null)
            {
                _ = defeated.Add(l.SongHashHex);
            }
        }

        return defeated;
    }
}
