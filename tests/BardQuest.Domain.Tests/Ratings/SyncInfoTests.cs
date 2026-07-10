using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class SyncInfoTests
{
    [Fact]
    public void OddMeterFraction_AllFourFour_IsZero()
    {
        var s = new SyncInfo(480, [new(0.0, 4, 4)]);
        Assert.Equal(0.0, s.OddMeterFraction(100.0), 6);
    }

    [Fact]
    public void OddMeterFraction_HalfInSeven_IsHalf()
    {
        var s = new SyncInfo(480,
        [
            new(0.0, 4, 4),   // 0..50
            new(50.0, 7, 8),  // 50..100  (odd)
        ]);
        Assert.Equal(0.5, s.OddMeterFraction(100.0), 6);
    }

    [Fact]
    public void OddMeterFraction_EmptyOrZeroDuration_IsZero()
    {
        Assert.Equal(0.0, new SyncInfo(480, []).OddMeterFraction(100.0));
        Assert.Equal(0.0, new SyncInfo(480, [new(0.0, 7, 8)]).OddMeterFraction(0.0));
    }
}
