using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class NoteDensityTests
{
    private static List<double> Steady(int count, double rate)
        => [.. Enumerable.Range(0, count).Select(i => i / rate)];

    [Fact]
    public void AvgNps_IsCountOverDuration()
        => Assert.Equal(5.0, NoteDensity.AvgNps(50, 10.0), 6);

    [Fact]
    public void AvgNps_ZeroOrNegativeDuration_IsZero()
    {
        Assert.Equal(0.0, NoteDensity.AvgNps(10, 0.0));
        Assert.Equal(0.0, NoteDensity.AvgNps(10, -3.0));
    }

    [Fact]
    public void PeakNps_SteadyStream_IsAboutTheRate()
        => Assert.True(Math.Abs(NoteDensity.PeakNps(Steady(80, 8.0)) - 8.0) <= 0.5);

    [Fact]
    public void PeakNps_FewerThanTwoNotes_IsZero()
        => Assert.Equal(0.0, NoteDensity.PeakNps([1.0]));

    [Fact]
    public void PeakWindowNps_CapturesShortBurst()
    {
        var times = new List<double>();
        times.AddRange(Steady(8, 2.0));                       // 0..3.5s slow
        times.AddRange(Enumerable.Range(0, 12).Select(i => 4.0 + (i / 24.0))); // 24/s burst
        times.Sort();
        Assert.True(NoteDensity.PeakWindowNps(times, 0.5) >= 18.0);
    }
}
