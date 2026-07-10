using BardQuest.Domain.Ratings;
using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumAttributeDerivationTests
{
    private static DrumRawMetrics Raw(
        double avg = 0, double peak = 0, double dense = 0, double kickDen = 0, int kickRun = 0,
        double burst = 0, double fill = 0, double gap = 1, double sync = 0, double odd = 0,
        double variety = 0, double offCarrier = 0, double offCarrierFast = 0,
        double residualAlt = 0, double noCarrierAlt = 0, double kickSpan = 0, double kitEntropy = 0)
        => new(avg, peak, dense, kickDen, kickRun, burst, fill, gap, sync, odd, variety,
            offCarrier, offCarrierFast, residualAlt, noCarrierAlt, kickSpan, kitEntropy);

    [Fact]
    public void AllZeroRaw_DerivesAllZeroScores()
    {
        AttributeProfile p = DrumAttributeDerivation.Derive(Raw());
        foreach (Attribute a in Enum.GetValues<Attribute>())
        {
            Assert.Equal(0.0, p[a], 6);
        }
    }

    [Fact]
    public void ScoresAreClampedToTen()
    {
        AttributeProfile p = DrumAttributeDerivation.Derive(Raw(
            avg: 99, peak: 99, dense: 99, kickDen: 99, kickRun: 999,
            burst: 99, fill: 99, gap: 0, sync: 1, odd: 1,
            variety: 1, offCarrier: 99, offCarrierFast: 99, residualAlt: 99, noCarrierAlt: 99, kickSpan: 99, kitEntropy: 99));
        foreach (Attribute a in Enum.GetValues<Attribute>())
        {
            Assert.InRange(p[a], 0.0, 10.0);
        }
    }

    [Fact]
    public void Strength_IsMonotonicInPeakNps()
    {
        double low = DrumAttributeDerivation.Derive(Raw(peak: 6))[Attribute.Strength];
        double high = DrumAttributeDerivation.Derive(Raw(peak: 16))[Attribute.Strength];
        Assert.True(high > low);
    }

    [Fact]
    public void Endurance_IsMonotonicInKickRun()
    {
        double low = DrumAttributeDerivation.Derive(Raw(kickRun: 4))[Attribute.Endurance];
        double high = DrumAttributeDerivation.Derive(Raw(kickRun: 28))[Attribute.Endurance];
        Assert.True(high > low);
    }

    [Fact]
    public void Endurance_TracksTheFastestKickSpan()
    {
        // FastestKickSpanNps is Endurance's burst term (it replaced the pruned quantised windowed metric).
        double low = DrumAttributeDerivation.Derive(Raw(kickSpan: 5))[Attribute.Endurance];
        double high = DrumAttributeDerivation.Derive(Raw(kickSpan: 7))[Attribute.Endurance];
        Assert.True(high > low);
    }

    [Fact]
    public void Agility_RewardsSmallerTransitionGap()
    {
        double slow = DrumAttributeDerivation.Derive(Raw(gap: 0.14))[Attribute.Agility];
        double fast = DrumAttributeDerivation.Derive(Raw(gap: 0.04))[Attribute.Agility];
        Assert.True(fast > slow);
    }

    [Fact]
    public void Technique_TracksIndependenceEvents_NotRawDensity()
    {
        // A fast chart with no independence events (a driven backbeat) reads zero — raw density
        // alone never earns Technique.
        double backbeat = DrumAttributeDerivation.Derive(Raw(avg: 9))[Attribute.Technique];
        Assert.Equal(0.0, backbeat, 6);

        // More carrier-stripped events per second is more technical.
        double some = DrumAttributeDerivation.Derive(Raw(residualAlt: 0.5))[Attribute.Technique];
        double dense = DrumAttributeDerivation.Derive(Raw(residualAlt: 2.0))[Attribute.Technique];
        Assert.True(some > 0);
        Assert.True(dense > some);
    }

    [Fact]
    public void Technique_WeighsSlowCarrierOffbeatsBelowFastCarrierOnes()
    {
        // Off-carrier work under a slow shuffle counts, but less than under a driving 16th ostinato.
        double slow = DrumAttributeDerivation.Derive(Raw(offCarrier: 1.0))[Attribute.Technique];
        double fast = DrumAttributeDerivation.Derive(Raw(offCarrier: 1.0, offCarrierFast: 1.0))[Attribute.Technique];
        Assert.True(slow > 0);
        Assert.True(fast > slow);
    }

    [Fact]
    public void Precision_IsWorseOfSyncopationOrOddMeter()
    {
        // Timing-exactness = the worse of syncopation load or odd-meter load (a MAX). Either alone
        // can drive it high.
        double syncOnly = DrumAttributeDerivation.Derive(Raw(sync: 0.40))[Attribute.Precision];
        double oddOnly = DrumAttributeDerivation.Derive(Raw(odd: 0.50))[Attribute.Precision];
        Assert.Equal(10.0, syncOnly, 6);   // sync at its ceiling alone maxes Precision
        Assert.Equal(10.0, oddOnly, 6);    // odd meter at its ceiling alone maxes Precision

        // The max picks the stronger signal: a modestly syncopated chart reads by whichever is worse.
        double mildSync = DrumAttributeDerivation.Derive(Raw(sync: 0.20))[Attribute.Precision];
        double mildSyncBigOdd = DrumAttributeDerivation.Derive(Raw(sync: 0.20, odd: 0.40))[Attribute.Precision];
        Assert.True(mildSyncBigOdd > mildSync);
    }

    [Fact]
    public void Dexterity_IsKitBreadthGatedByNonRepetition()
    {
        // Kit-ranging = breadth (KitPieceEntropy) AND non-repetition (PatternVariety). At equal variety,
        // wider piece-spread scores higher; but a repetitive chart is suppressed however many pieces it
        // touches — the White Stripes case: broad but looping ranks below a varied narrower chart.
        double narrowVaried = DrumAttributeDerivation.Derive(Raw(kitEntropy: 1.0, variety: 0.6))[Attribute.Dexterity];
        double wideVaried = DrumAttributeDerivation.Derive(Raw(kitEntropy: 2.2, variety: 0.6))[Attribute.Dexterity];
        double wideRepetitive = DrumAttributeDerivation.Derive(Raw(kitEntropy: 2.2, variety: 0.05))[Attribute.Dexterity];
        Assert.True(wideVaried > narrowVaried);      // more breadth = more Dexterity, at equal variety
        Assert.True(wideVaried > wideRepetitive);    // repetition suppresses breadth (the White Stripes fix)
        Assert.True(wideRepetitive < narrowVaried);  // a looping broad chart ranks below a varied narrow one
    }
}
