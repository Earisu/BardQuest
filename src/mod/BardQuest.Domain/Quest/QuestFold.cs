using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Domain.Quest;

/// <summary>The extended chronological fold — B's per-axis XP fold run under the quest gate rules.
/// Walking plays in date order it maintains XP/levels AND the unlocked-gate step together. Normal play
/// uses B's ~0.85 knee; ONLY while xpStanding presses an unbroken gate does the raised bar apply
/// (sub-bar plays earn nothing — anti-farm). A gate advances when a play clears its condition: a
/// same-rank monster at the mini-boss bar, or a next-floor monster at a plain clear for a class boss.
/// Pure and retune-safe: same links → same state.</summary>
public static class QuestFold
{
    private const double Eps = 1e-9;

    private static readonly Attribute[] Axes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    public static QuestState Run(
        IReadOnlyList<(AttributeProfile Song, PerformanceFacts Perf)> playsInDateOrder, QuestPace pace)
    {
        var curve = LevelCurve.ForPace(pace);
        var xp = new Dictionary<Attribute, double>(Axes.Length);
        var level = new Dictionary<Attribute, int>(Axes.Length);
        foreach (Attribute a in Axes)
        {
            xp[a] = 0;
            level[a] = 0;
        }

        int gates = 0;

        foreach ((AttributeProfile song, PerformanceFacts perf) in playsInDateOrder)
        {
            int xpStep = QuestLadder.StepForScore(Sum(level));
            bool pressing = xpStep > gates && gates < QuestLadder.TopStep;
            double bar = pressing ? GateBar(gates) : AttributeXpFormula.ClearThreshold;

            if (perf.Percent + Eps < bar)
            {
                continue; // sub-bar while pressing (or a genuine non-clear) → no XP, no gate
            }

            Dictionary<Attribute, double> award = AttributeXpFormula.Award(song, perf, level);
            foreach (Attribute a in Axes)
            {
                xp[a] += award[a];
                level[a] = curve.LevelFor(xp[a]);
            }

            // A mini-boss advances only while pressing (XP wants past this subrank). A class boss
            // advances once you've REACHED the gate (its top subrank) and clear a next-floor monster —
            // NOT only while pressing: honest XP can't be required to already exceed the class ceiling
            // (per-axis levels cap, and that crossing is exactly what the boss gates). Effective standing
            // is unchanged — still min(xpStanding, gatesUnlocked) — so the unlocked gate just lets the
            // next class's content in; class follows once XP climbs (decision (b), not a promotion).
            bool eligible = QuestLadder.IsClassBossGate(gates)
                ? gates < QuestLadder.TopStep && xpStep >= gates
                : pressing;
            if (eligible && Qualifies(gates, song.Sum(), perf.Percent))
            {
                gates++;
            }
        }

        PlayerProfile profile = BuildProfile(xp, level);
        int effective = Math.Min(QuestLadder.StepForScore(profile.Score), gates);
        bool complete = effective >= QuestLadder.StepIndex(PlayerClass.Legendweaver, 0);
        return new QuestState(profile, gates, effective, complete);
    }

    // Sum of the five current levels — the 0–50 axis both classes and chart ranks live on.
    private static double Sum(IReadOnlyDictionary<Attribute, int> level)
    {
        double total = 0;
        foreach (Attribute a in Axes)
        {
            total += level[a];
        }

        return total;
    }

    // The clear bar to cross gate `step`: a plain clear for a class boss, the escalating bar for a mini-boss.
    private static double GateBar(int step)
        => QuestLadder.IsClassBossGate(step)
            ? AttributeXpFormula.ClearThreshold
            : QuestLadder.MiniBossBar(QuestLadder.ClassOfStep(step));

    // Does a play of a monster (RankScore `sum`, `percent`) satisfy gate `step`?
    private static bool Qualifies(int step, double sum, double percent)
    {
        PlayerClass band = QuestLadder.ClassOfStep(step);
        (double _, double nextFloor) = ClassDerivation.Range(band);
        return QuestLadder.IsClassBossGate(step)
            ? sum >= nextFloor && percent + Eps >= AttributeXpFormula.ClearThreshold   // harder song, plain clear
            : sum < nextFloor && percent + Eps >= QuestLadder.MiniBossBar(band);        // same-rank, escalating bar
    }

    private static PlayerProfile BuildProfile(
        IReadOnlyDictionary<Attribute, double> xp, IReadOnlyDictionary<Attribute, int> level)
    {
        var axes = new Dictionary<Attribute, AttributeState>(Axes.Length);
        foreach (Attribute a in Axes)
        {
            axes[a] = new AttributeState(xp[a], level[a]);
        }

        return new PlayerProfile(axes);
    }
}
