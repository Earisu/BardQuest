using BardQuest.Domain.Ratings;
using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumRawMetricsDerivationTests
{
    private static DrumRawMetrics Raw(
        double avg = 0, double peak = 0, double dense = 0, double kickDen = 0, int kickRun = 0,
        double burst = 0, double fill = 0, double gap = 1,
        double variety = 0, double offCarrier = 0, double offCarrierFast = 0,
        double residualAlt = 0, double noCarrierAlt = 0, double kickSpan = 0, double kitEntropy = 0)
        => new(avg, peak, dense, kickDen, kickRun, burst, fill, gap, variety,
            offCarrier, offCarrierFast, residualAlt, noCarrierAlt, kickSpan, kitEntropy);

    [Fact]
    public void AllZeroRaw_DerivesAllZeroScores()
    {
        var p = Raw().ToAttributeProfile();
        foreach (Attribute a in Enum.GetValues<Attribute>())
        {
            Assert.Equal(0.0, p[a], 6);
        }
    }

    [Fact]
    public void ScoresAreClampedToTen()
    {
        var p = Raw(
            avg: 99, peak: 99, dense: 99, kickDen: 99, kickRun: 999,
            burst: 99, fill: 99, gap: 0,
            variety: 1, offCarrier: 99, offCarrierFast: 99, residualAlt: 99, noCarrierAlt: 99, kickSpan: 99, kitEntropy: 99).ToAttributeProfile();
        foreach (Attribute a in Enum.GetValues<Attribute>())
        {
            Assert.InRange(p[a], 0.0, 10.0);
        }
    }

    [Fact]
    public void Strength_IsMonotonicInPeakNps()
    {
        double low = Raw(peak: 6).ToAttributeProfile()[Attribute.Strength];
        double high = Raw(peak: 16).ToAttributeProfile()[Attribute.Strength];
        Assert.True(high > low);
    }

    [Fact]
    public void Endurance_IsMonotonicInKickRun()
    {
        double low = Raw(kickRun: 4).ToAttributeProfile()[Attribute.Endurance];
        double high = Raw(kickRun: 28).ToAttributeProfile()[Attribute.Endurance];
        Assert.True(high > low);
    }

    [Fact]
    public void Endurance_TracksTheFastestKickSpan()
    {
        // FastestKickSpanNps is Endurance's burst term (it replaced the pruned quantised windowed metric).
        double low = Raw(kickSpan: 5).ToAttributeProfile()[Attribute.Endurance];
        double high = Raw(kickSpan: 7).ToAttributeProfile()[Attribute.Endurance];
        Assert.True(high > low);
    }

    [Fact]
    public void Agility_RewardsSmallerTransitionGap()
    {
        double slow = Raw(gap: 0.14).ToAttributeProfile()[Attribute.Agility];
        double fast = Raw(gap: 0.04).ToAttributeProfile()[Attribute.Agility];
        Assert.True(fast > slow);
    }

    [Fact]
    public void Technique_TracksIndependenceEvents_NotRawDensity()
    {
        // A fast chart with no independence events (a driven backbeat) reads zero — raw density
        // alone never earns Technique.
        double backbeat = Raw(avg: 9).ToAttributeProfile()[Attribute.Technique];
        Assert.Equal(0.0, backbeat, 6);

        // More carrier-stripped events per second is more technical.
        double some = Raw(residualAlt: 0.5).ToAttributeProfile()[Attribute.Technique];
        double dense = Raw(residualAlt: 2.0).ToAttributeProfile()[Attribute.Technique];
        Assert.True(some > 0);
        Assert.True(dense > some);
    }

    [Fact]
    public void Technique_WeighsSlowCarrierOffbeatsBelowFastCarrierOnes()
    {
        // Off-carrier work under a slow shuffle counts, but less than under a driving 16th ostinato.
        double slow = Raw(offCarrier: 1.0).ToAttributeProfile()[Attribute.Technique];
        double fast = Raw(offCarrier: 1.0, offCarrierFast: 1.0).ToAttributeProfile()[Attribute.Technique];
        Assert.True(slow > 0);
        Assert.True(fast > slow);
    }

    [Fact]
    public void Dexterity_IsKitBreadthGatedByNonRepetition()
    {
        // Kit-ranging = breadth (KitPieceEntropy) AND non-repetition (PatternVariety). At equal variety,
        // wider piece-spread scores higher; but a repetitive chart is suppressed however many pieces it
        // touches — the White Stripes case: broad but looping ranks below a varied narrower chart.
        double narrowVaried = Raw(kitEntropy: 1.0, variety: 0.6).ToAttributeProfile()[Attribute.Dexterity];
        double wideVaried = Raw(kitEntropy: 2.2, variety: 0.6).ToAttributeProfile()[Attribute.Dexterity];
        double wideRepetitive = Raw(kitEntropy: 2.2, variety: 0.05).ToAttributeProfile()[Attribute.Dexterity];
        Assert.True(wideVaried > narrowVaried);      // more breadth = more Dexterity, at equal variety
        Assert.True(wideVaried > wideRepetitive);    // repetition suppresses breadth (the White Stripes fix)
        Assert.True(wideRepetitive < narrowVaried);  // a looping broad chart ranks below a varied narrow one
    }
}
