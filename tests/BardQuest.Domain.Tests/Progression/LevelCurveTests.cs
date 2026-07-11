using BardQuest.Domain.Progression;

using Xunit;

namespace BardQuest.Domain.Tests.Progression;

public class LevelCurveTests
{
    private static readonly LevelCurve Journey = LevelCurve.ForPace(QuestPace.Journey);

    [Fact]
    public void ZeroXpIsLevelZero() => Assert.Equal(0, Journey.LevelFor(0));

    [Fact]
    public void LevelForIsMonotonicAndClampedAtMax()
    {
        Assert.Equal(5, Journey.LevelFor(Journey.CumulativeXpFor(5)));
        Assert.Equal(4, Journey.LevelFor(Journey.CumulativeXpFor(5) - 1));
        Assert.Equal(LevelCurve.MaxLevel, Journey.LevelFor(double.MaxValue));
    }

    [Fact]
    public void CostGrowsWithLevel()
    {
        double lowStep = Journey.CumulativeXpFor(2) - Journey.CumulativeXpFor(1);
        double highStep = Journey.CumulativeXpFor(9) - Journey.CumulativeXpFor(8);
        Assert.True(highStep > lowStep);
    }

    [Fact]
    public void SlowerPaceCostsMorePerLevel()
    {
        double sprint = LevelCurve.ForPace(QuestPace.Sprint).CumulativeXpFor(5);
        double journey = LevelCurve.ForPace(QuestPace.Journey).CumulativeXpFor(5);
        double odyssey = LevelCurve.ForPace(QuestPace.Odyssey).CumulativeXpFor(5);
        Assert.True(sprint < journey && journey < odyssey);
    }

    [Fact]
    public void ProgressReportsIntoAndNeeded()
    {
        double floor5 = Journey.CumulativeXpFor(5);
        (int level, double into, double needed) = Journey.Progress(floor5 + 10);
        Assert.Equal(5, level);
        Assert.Equal(10, into, 6);
        Assert.True(needed > 0);
        Assert.Equal(0, Journey.Progress(double.MaxValue).Needed);
    }
}
