using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;

using Xunit;

namespace BardQuest.Domain.Tests.Quest;

public class QuestLadderTests
{
    [Fact]
    public void StepIndexRoundTrips()
    {
        for (int step = 0; step <= QuestLadder.TopStep; step++)
        {
            PlayerClass cls = QuestLadder.ClassOfStep(step);
            int sub = QuestLadder.SubrankOfStep(step);
            Assert.Equal(step, QuestLadder.StepIndex(cls, sub));
        }
    }

    [Fact]
    public void LadderSpansEighteenSteps()
    {
        Assert.Equal(18, QuestLadder.StepCount);
        Assert.Equal(17, QuestLadder.TopStep);
        Assert.Equal(0, QuestLadder.StepForScore(0));                    // Busker I
        Assert.Equal(17, QuestLadder.StepForScore(50));                  // Legendweaver III
    }

    [Fact]
    public void MiniBossBarEscalatesByClass()
    {
        Assert.Equal(0.85, QuestLadder.MiniBossBar(PlayerClass.Busker));
        Assert.Equal(0.90, QuestLadder.MiniBossBar(PlayerClass.Bard));
        Assert.Equal(0.95, QuestLadder.MiniBossBar(PlayerClass.Skald));
    }

    [Fact]
    public void ClassBossGateIsTheThirdSubrankBoundary()
    {
        Assert.True(QuestLadder.IsClassBossGate(QuestLadder.StepIndex(PlayerClass.Busker, 2)));  // III → next class
        Assert.False(QuestLadder.IsClassBossGate(QuestLadder.StepIndex(PlayerClass.Busker, 0))); // I → II
        Assert.False(QuestLadder.IsClassBossGate(QuestLadder.StepIndex(PlayerClass.Busker, 1))); // II → III
    }

    [Fact]
    public void RangeCoversTheBands()
    {
        Assert.Equal((0.0, 12.0), ClassDerivation.Range(PlayerClass.Busker));
        Assert.Equal((36.0, 43.0), ClassDerivation.Range(PlayerClass.Skald));
        Assert.Equal((43.0, 50.0), ClassDerivation.Range(PlayerClass.Legendweaver));
    }
}
