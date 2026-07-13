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

        // 3. Effective class/subrank from the clamped step; are we at an exclusive class-boss phase?
        PlayerClass cls = QuestLadder.ClassOfStep(state.EffectiveStep);
        int subrank = QuestLadder.SubrankOfStep(state.EffectiveStep);
        bool atBoss = state.GatesUnlocked < QuestLadder.TopStep
                   && QuestLadder.IsClassBossGate(state.GatesUnlocked)
                   && QuestLadder.StepForScore(state.Profile.Score) > state.GatesUnlocked;

        // 4. Best percent per hash (for the "defeated" flag), from the resolved plays.
        var bestPercent = new Dictionary<string, double>();
        foreach ((AttributeProfile _, PerformanceFacts facts, DateTime _, string hash) in resolved)
        {
            bestPercent[hash] = Math.Max(bestPercent.TryGetValue(hash, out double p) ? p : 0, facts.Percent);
        }

        // 5. Build delivery statuses. During a boss phase only the boss is shown.
        double miniBar = QuestLadder.MiniBossBar(cls);
        MonsterStatus? boss = BuildBoss(quest.Delivery.BossHash, library, bestPercent);
        List<MonsterStatus> working = atBoss
            ? []
            : BuildWorkingSet(quest.Delivery.WorkingSet, library, bestPercent, miniBar);

        return new ActiveQuestView(
            state.Profile, cls, subrank, state.EffectiveStep, state.IsComplete, atBoss, working, boss);
    }

    private static List<MonsterStatus> BuildWorkingSet(
        IReadOnlyList<string> hashes, RatedLibrary library, IReadOnlyDictionary<string, double> bestPercent, double miniBar)
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
            monsters.Add(new MonsterStatus(hash, profile, profile.Sum(), defeated, IsMiniBoss: false, IsBoss: false));
        }

        // Highlight the hardest in-set monster as the mini-boss (the concrete same-rank target).
        if (monsters.Count > 0)
        {
            int top = 0;
            for (int i = 1; i < monsters.Count; i++)
            {
                if (monsters[i].Sum > monsters[top].Sum)
                {
                    top = i;
                }
            }

            monsters[top] = monsters[top] with { IsMiniBoss = true };
        }

        return monsters;
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
        return new MonsterStatus(bossHash, profile, profile.Sum(), defeated, IsMiniBoss: false, IsBoss: true);
    }
}
