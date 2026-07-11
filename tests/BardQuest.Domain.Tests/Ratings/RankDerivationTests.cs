using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class RankDerivationTests
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
    public void AllZero_IsF() => Assert.Equal(Rank.F, RankDerivation.Derive(Flat(0)));

    [Fact]
    public void AllFiveTens_IsSSS() => Assert.Equal(Rank.SSS, RankDerivation.Derive(Flat(10)));

    [Fact]
    public void VarietyBonus_LiftsAVariedChartOverABandEdge()
    {
        // Flat(5.5) -> sum 27.5, mid-C; a fully varied chart gets the nudge into B.
        Assert.Equal(Rank.C, RankDerivation.Derive(Flat(5.5)));
        Assert.Equal(Rank.B, RankDerivation.Derive(Flat(5.5), patternVariety: 1.0));
    }

    [Fact]
    public void VarietyBonus_NeverSinks_AndIgnoresTheLoopingHalf()
    {
        // At/below the floor (a chart looping one bar) the bonus is exactly zero — a hard loop
        // (Everlong) keeps its rank, and no variety value can ever lower a rank.
        Assert.Equal(
            RankDerivation.Derive(Flat(8)),
            RankDerivation.Derive(Flat(8), patternVariety: RankDerivation.VarietyFloor));
        Assert.Equal(
            RankDerivation.Derive(Flat(8)),
            RankDerivation.Derive(Flat(8), patternVariety: 0.0));
    }

    [Fact]
    public void VarietyBonus_IsCappedAtTheCeiling()
        => Assert.Equal(
            RankDerivation.Derive(Flat(6), patternVariety: RankDerivation.VarietyCeil),
            RankDerivation.Derive(Flat(6), patternVariety: 5.0));

    [Fact]
    public void RankUsesDexterity()
        // Dexterity is now a rank axis: at equal physical axes, more Dexterity lifts the rank.
        => Assert.True(RankDerivation.Derive(Of(5, 5, 5, 5, dex: 9)) > RankDerivation.Derive(Of(5, 5, 5, 5, dex: 1)));

    [Fact]
    public void BossRule_PromotesAOneAxisSpecialist()
    {
        // Flat(6.5) -> sum 32.5 -> B, no spike. The spiked chart (sum 32 -> B) maxes an axis and climbs to A.
        Assert.Equal(Rank.B, RankDerivation.Derive(Flat(6.5)));                 // no spike, stays B
        Assert.Equal(Rank.A, RankDerivation.Derive(Of(9, 6, 6, 6, dex: 5)));   // Strength spike -> promoted
    }

    [Fact]
    public void BossRule_DoesNotPromoteBelowB() =>
        // Sum 21 -> D; a lone spike must NOT promote a chart that isn't already B+.
        Assert.Equal(Rank.D, RankDerivation.Derive(Of(9, 3, 3, 3, dex: 3)));

    [Fact]
    public void BossRule_CanReachSSS() =>
        // Flat(9) -> sum 45 -> SS; the maxed axis promotes it to SSS.
        Assert.Equal(Rank.SSS, RankDerivation.Derive(Flat(9)));

    [Fact]
    public void RankIsMonotonicInMean()
    {
        Assert.True(RankDerivation.Derive(Flat(2)) <= RankDerivation.Derive(Flat(5)));
        Assert.True(RankDerivation.Derive(Flat(5)) <= RankDerivation.Derive(Flat(9)));
    }
}
