// src/mod/BardQuest.Domain/Quest/QuestEngine.cs
using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

namespace BardQuest.Domain.Quest;

/// <summary>The read path: resolves a quest's provenance links against the rated library and score
/// source, folds them (<see cref="QuestFold"/>), and projects the boss-clamped standing + live delivery
/// statuses into an <see cref="ActiveQuestView"/>. Pure given its inputs.</summary>
public static class QuestEngine
{
    public static ActiveQuestView Resolve(Quest quest, RatedLibrary library, IScoreSource scores)
    {
        // 1. Resolve each link to (profile, facts, date); drop any that don't resolve (dangling link /
        //    unrated song). Sort by play date for the chronological fold.
        var resolved = new List<(AttributeProfile Profile, PerformanceFacts Facts, DateTime When, string Hash)>();
        foreach (ProvenanceLink link in quest.Links)
        {
            AttributeProfile? profile = library.Profile(link.SongHashHex);
            PerformanceFacts? facts = scores.Resolve(link);
            if (profile != null && facts != null)
            {
                resolved.Add((profile, facts, link.PlayedAt, link.SongHashHex));
            }
        }

        resolved.Sort((x, y) => x.When.CompareTo(y.When));

        // 2. Fold under the gate rules.
        QuestState state = QuestFold.Run(
            resolved.Select(r => (r.Profile, r.Facts)).ToList(), quest.Pace);

        // 3. Effective class/subrank from the clamped step. "Pressing" a gate = the honest XP standing
        //    wants past a still-locked gate — the breakthrough moment. A pressed class-boss gate is the
        //    exclusive boss phase; a pressed mini-boss gate is the exclusive Elite phase. Off a pressed
        //    gate it is the ordinary grind (regular working set, no Elite).
        PlayerClass cls = QuestLadder.ClassOfStep(state.EffectiveStep);
        int subrank = QuestLadder.SubrankOfStep(state.EffectiveStep);
        bool pressing = state.GatesUnlocked < QuestLadder.TopStep
                     && QuestLadder.StepForScore(state.Profile.Score) > state.GatesUnlocked;
        bool atBoss = pressing && QuestLadder.IsClassBossGate(state.GatesUnlocked);
        bool atMiniBoss = pressing && !QuestLadder.IsClassBossGate(state.GatesUnlocked);

        // 4. Best percent per hash (for the "defeated" flag), from the resolved plays. Case-insensitive:
        //    link hashes are lowercase but the working set is keyed uppercase (see RatedLibrary) — a
        //    case-sensitive map here would never mark a played monster "cleared".
        var bestPercent = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach ((AttributeProfile _, PerformanceFacts facts, DateTime _, string hash) in resolved)
        {
            bestPercent[hash] = Math.Max(bestPercent.TryGetValue(hash, out double p) ? p : 0, facts.Percent);
        }

        // 5. Build delivery statuses. During a boss phase only the boss is shown; during a mini-boss
        //    breakthrough only the Elite is shown; otherwise the ordinary regular working set.
        double miniBar = QuestLadder.MiniBossBar(cls);
        MonsterStatus? boss = BuildBoss(quest.Delivery.BossHash, library, bestPercent);
        List<MonsterStatus> working = atBoss
            ? []
            : BuildWorkingSet(quest.Delivery.WorkingSet, library, bestPercent, miniBar, atMiniBoss);

        return new ActiveQuestView(
            state.Profile, cls, subrank, state.EffectiveStep, state.IsComplete, atBoss, atMiniBoss, working, boss);
    }

    private static List<MonsterStatus> BuildWorkingSet(
        IReadOnlyList<string> hashes, RatedLibrary library, IReadOnlyDictionary<string, double> bestPercent,
        double miniBar, bool miniBossPhase)
    {
        var monsters = new List<MonsterStatus>();
        foreach (string hash in hashes)
        {
            AttributeProfile? profile = library.Profile(hash);
            if (profile == null)
            {
                continue;
            }

            bool defeated = bestPercent.TryGetValue(hash, out double p) && p + 1e-9 >= miniBar;
            monsters.Add(new MonsterStatus(hash, profile, profile.Sum(), defeated, MonsterType.Regular));
        }

        if (monsters.Count == 0)
        {
            return monsters;
        }

        // The hardest in-set monster is the mini-boss — the concrete same-rank breakthrough target.
        int top = 0;
        for (int i = 1; i < monsters.Count; i++)
        {
            if (monsters[i].Sum > monsters[top].Sum)
            {
                top = i;
            }
        }

        // Gate the Elite like a class boss: it appears ONLY during the breakthrough (mini-boss phase),
        // and then it is the whole delivery — no regular monsters alongside it. Off the breakthrough the
        // player just grinds XP against the regular set, with no Elite highlighted yet.
        return miniBossPhase
            ? [monsters[top] with { Type = MonsterType.Elite }]
            : monsters;
    }

    private static MonsterStatus? BuildBoss(
        string? bossHash, RatedLibrary library, IReadOnlyDictionary<string, double> bestPercent)
    {
        if (bossHash == null)
        {
            return null;
        }

        AttributeProfile? profile = library.Profile(bossHash);
        if (profile == null)
        {
            return null;
        }

        bool defeated = bestPercent.TryGetValue(bossHash, out double p) && p + 1e-9 >= AttributeXpFormula.ClearThreshold;
        return new MonsterStatus(bossHash, profile, profile.Sum(), defeated, MonsterType.Boss);
    }
}
