// tests/BardQuest.Domain.Tests/Quest/QuestFoldTests.cs
using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Quest;

public class QuestFoldTests
{
    private static AttributeProfile Song(double sum)
        => new(new Dictionary<Attribute, double> { [Attribute.Strength] = sum });

    private static PerformanceFacts Perf(double percent, bool fc = false)
        => new(percent, fc, Stars: 5, NotesHit: 100, NotesMissed: 0, Difficulty.Expert);

    private static (AttributeProfile, PerformanceFacts) Play(double sum, double percent = 1.0)
        => (Song(sum), Perf(percent));

    [Fact]
    public void EmptyQuestIsStepZeroNotComplete()
    {
        QuestState s = QuestFold.Run([], QuestPace.Journey);
        Assert.Equal(0, s.GatesUnlocked);
        Assert.Equal(0, s.EffectiveStep);
        Assert.False(s.IsComplete);
        Assert.Equal(PlayerClass.Busker, s.Profile.Class);
    }

    [Fact]
    public void ClearingBuskerBandMonstersAdvancesGates()
    {
        // Many Busker-band (sum ~6, < the 12 Minstrel floor) full-percent clears: XP climbs and each
        // subrank breakthrough is a same-rank clear >= 0.85, so gates advance beyond step 0.
        var plays = Enumerable.Repeat(Play(sum: 6, percent: 1.0), 30).ToList();
        QuestState s = QuestFold.Run(plays, QuestPace.Sprint);
        Assert.True(s.GatesUnlocked >= 1);
    }

    [Fact]
    public void EffectiveStepNeverExceedsGatesUnlocked()
    {
        var plays = Enumerable.Repeat(Play(sum: 6, percent: 1.0), 60).ToList();
        QuestState s = QuestFold.Run(plays, QuestPace.Sprint);
        Assert.True(s.EffectiveStep <= s.GatesUnlocked);
    }

    [Fact]
    public void CannotCrossAClassBossGateWithoutClearingANextFloorMonster()
    {
        // Enough same-rank Busker clears to press the Busker III -> Minstrel class boss (step 2), but
        // NEVER a monster reaching the Minstrel floor (12). Gates must stall at the class boundary.
        var buskerGrind = Enumerable.Repeat(Play(sum: 10, percent: 1.0), 80).ToList();
        QuestState stalled = QuestFold.Run(buskerGrind, QuestPace.Sprint);
        Assert.Equal(QuestLadder.StepIndex(PlayerClass.Busker, 2), stalled.GatesUnlocked); // stuck at Busker III

        // Now append one boss clear (a Minstrel-floor monster, sum 13, plain clear) → the gate opens.
        var withBoss = new List<(AttributeProfile, PerformanceFacts)>(buskerGrind) { Play(sum: 13, percent: 0.90) };
        QuestState advanced = QuestFold.Run(withBoss, QuestPace.Sprint);
        Assert.True(advanced.GatesUnlocked > stalled.GatesUnlocked);
    }

    [Fact]
    public void SubBarPlaysWhilePressingEarnNoXpAntiFarm()
    {
        // Climb into the pressing state on Busker (I->II mini-boss, bar 0.85) with a strong clear,
        // then spam 0.80 clears: they must add no XP and not advance the gate.
        var climb = Enumerable.Repeat(Play(sum: 6, percent: 1.0), 8).ToList();
        QuestState pressing = QuestFold.Run(climb, QuestPace.Sprint);

        var farm = new List<(AttributeProfile, PerformanceFacts)>(climb);
        farm.AddRange(Enumerable.Repeat(Play(sum: 6, percent: 0.80), 20)); // below the 0.85 mini-boss bar
        QuestState farmed = QuestFold.Run(farm, QuestPace.Sprint);

        Assert.Equal(pressing.Profile.Score, farmed.Profile.Score, 6); // no XP gained from sub-bar farming
    }

    [Fact]
    public void FoldIsDeterministic()
    {
        var plays = Enumerable.Repeat(Play(sum: 7, percent: 0.95), 15).ToList();
        Assert.Equal(QuestFold.Run(plays, QuestPace.Journey).Profile.Score,
                     QuestFold.Run(plays, QuestPace.Journey).Profile.Score, 6);
    }
}
