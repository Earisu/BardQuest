using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class ChartRatingCalculatorTests
{
    private static ChartDifficultyProfile P(double peak, double avg,
        double db = 0, double blast = 0, double indep = 0, double fill = 0)
        => new(peak, avg, db, blast, indep, fill);

    [Fact]
    public void SubScore_DensityDominatesTechnique()
    {
        double highDensityNoTech = ChartRatingCalculator.SubScore(P(16, 12));
        double lowDensityMaxTech = ChartRatingCalculator.SubScore(P(4, 3, 1, 1, 1, 1));
        Assert.True(highDensityNoTech > lowDensityMaxTech);
    }

    [Fact]
    public void SubScore_OrdersSteadyBelowSpikyBelowHardThroughout()
    {
        double steady = ChartRatingCalculator.SubScore(P(9, 8)); // A
        double spiky = ChartRatingCalculator.SubScore(P(18, 5)); // B
        double hardThroughout = ChartRatingCalculator.SubScore(P(18, 15)); // C
        Assert.True(steady < spiky);
        Assert.True(spiky < hardThroughout);
    }

    [Fact]
    public void SubScore_TechniqueRaisesAnOtherwiseIdenticalChart()
    {
        double plain = ChartRatingCalculator.SubScore(P(10, 8));
        double technical = ChartRatingCalculator.SubScore(P(10, 8, 1, 1, 1, 1));
        Assert.True(technical > plain);
    }

    [Fact]
    public void SubScore_NeverReachesOne()
    {
        double max = ChartRatingCalculator.SubScore(P(999, 999, 1, 1, 1, 1));
        Assert.True(max < 1.0);
        Assert.True(Math.Abs(max - 0.999) <= 0.0001);
    }

    [Fact]
    public void SubScore_AllZeroProfile_IsZero() => Assert.Equal(0.0, ChartRatingCalculator.SubScore(P(0, 0)));
}
