using BardQuest.Domain.Ratings;
using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumRawMetricsTests
{
    [Fact]
    public void Record_HoldsAllFields_AndIsIRawMetrics()
    {
        var m = new DrumRawMetrics(
            AvgNps: 5, PeakNps: 8, LongestDenseSectionSeconds: 3,
            KickDensity: 2, LongestKickRun: 12,
            PeakBurstNps: 18, FastFillRate: 12, ShortestTransitionGap: 0.06,
            SyncopationFraction: 0.4, OddMeterFraction: 0.1, PatternVariety: 0.6,
            OffCarrierPerSec: 1.1, OffCarrierFastPerSec: 0.8,
            ResidualAltPerSec: 2.3, NoCarrierAltPerSec: 0.5, FastestKickSpanNps: 6.8, KitPieceEntropy: 1.4);

        _ = Assert.IsAssignableFrom<IRawMetrics>(m);
        Assert.Equal(12, m.LongestKickRun);
        Assert.Equal(0.06, m.ShortestTransitionGap, 6);
        Assert.Equal(0.4, m.SyncopationFraction, 6);
        Assert.Equal(0.1, m.OddMeterFraction, 6);
        Assert.Equal(0.6, m.PatternVariety, 6);
        Assert.Equal(1.1, m.OffCarrierPerSec, 6);
        Assert.Equal(0.8, m.OffCarrierFastPerSec, 6);
        Assert.Equal(2.3, m.ResidualAltPerSec, 6);
        Assert.Equal(0.5, m.NoCarrierAltPerSec, 6);
        Assert.Equal(6.8, m.FastestKickSpanNps, 6);
        Assert.Equal(1.4, m.KitPieceEntropy, 6);
    }

    [Fact]
    public void Zero_IsAllZero()
    {
        Assert.Equal(0.0, DrumRawMetrics.Zero.PeakNps);
        Assert.Equal(0, DrumRawMetrics.Zero.LongestKickRun);
    }
}
