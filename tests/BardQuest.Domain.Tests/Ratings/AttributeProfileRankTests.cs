using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class AttributeProfileRankTests
{
    // Five axes drive the rank: Strength/Endurance/Technique/Agility/Dexterity.
    // Bands: F<11 E<18 D<24 C<29 B<34 A<38 S<43 SS<47, else SSS (score = five-axis sum).
    private static AttributeProfile Of(double str, double end, double tec, double agi, double dex = 0)
        => new(new Dictionary<Attribute, double>
        {
            [Attribute.Strength] = str,
            [Attribute.Endurance] = end,
            [Attribute.Technique] = tec,
            [Attribute.Agility] = agi,
            [Attribute.Dexterity] = dex,
        });

    // All five rank axes equal to v.
    private static AttributeProfile Flat(double v) => Of(v, v, v, v, dex: v);

    [Fact]
    public void AllZero_IsF() => Assert.Equal(Rank.F, Flat(0).ToRank());

    [Fact]
    public void AllFiveTens_IsSSS() => Assert.Equal(Rank.SSS, Flat(10).ToRank());

    [Fact]
    public void VarietyBonus_LiftsAVariedChartOverABandEdge()
    {
        // Flat(5.5) -> sum 27.5, mid-C; a fully varied chart gets the nudge into B.
        Assert.Equal(Rank.C, Flat(5.5).ToRank());
        Assert.Equal(Rank.B, Flat(5.5).ToRank(patternVariety: 1.0));
    }

    [Fact]
    public void VarietyBonus_NeverSinks_AndIgnoresTheLoopingHalf()
    {
        // At/below the floor (a chart looping one bar) the bonus is exactly zero — a hard loop
        // (Everlong) keeps its rank, and no variety value can ever lower a rank.
        Assert.Equal(
            Flat(8).ToRank(),
            Flat(8).ToRank(patternVariety: AttributeProfile.VarietyFloor));
        Assert.Equal(
            Flat(8).ToRank(),
            Flat(8).ToRank(patternVariety: 0.0));
    }

    [Fact]
    public void VarietyBonus_IsCappedAtTheCeiling()
        => Assert.Equal(
            Flat(6).ToRank(patternVariety: AttributeProfile.VarietyCeil),
            Flat(6).ToRank(patternVariety: 5.0));

    [Fact]
    public void RankUsesDexterity()
        // Dexterity is now a rank axis: at equal physical axes, more Dexterity lifts the rank.
        => Assert.True(Of(5, 5, 5, 5, dex: 9).ToRank() > Of(5, 5, 5, 5, dex: 1).ToRank());

    [Fact]
    public void BossRule_PromotesAOneAxisSpecialist()
    {
        // Flat(6.5) -> sum 32.5 -> B, no spike. The spiked chart (sum 32 -> B) maxes an axis and climbs to A.
        Assert.Equal(Rank.B, Flat(6.5).ToRank());                 // no spike, stays B
        Assert.Equal(Rank.A, Of(9, 6, 6, 6, dex: 5).ToRank());   // Strength spike -> promoted
    }

    [Fact]
    public void BossRule_DoesNotPromoteBelowB() =>
        // Sum 21 -> D; a lone spike must NOT promote a chart that isn't already B+.
        Assert.Equal(Rank.D, Of(9, 3, 3, 3, dex: 3).ToRank());

    [Fact]
    public void BossRule_CanReachSSS() =>
        // Flat(9) -> sum 45 -> SS; the maxed axis promotes it to SSS.
        Assert.Equal(Rank.SSS, Flat(9).ToRank());

    [Fact]
    public void RankIsMonotonicInMean()
    {
        Assert.True(Flat(2).ToRank() <= Flat(5).ToRank());
        Assert.True(Flat(5).ToRank() <= Flat(9).ToRank());
    }
}
