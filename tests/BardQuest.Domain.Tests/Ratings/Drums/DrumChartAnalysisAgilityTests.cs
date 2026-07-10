using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalysisAgilityTests
{
    [Fact]
    public void PeakBurstNps_CapturesShortFastCluster()
    {
        var notes = new List<RoleNote>();
        notes.AddRange(Enumerable.Range(0, 6).Select(i => new RoleNote(i / 2.0, DrumRole.Snare, 0u)));    // slow
        notes.AddRange(Enumerable.Range(0, 12).Select(i => new RoleNote(4.0 + (i / 24.0), DrumRole.Tom, 0u))); // burst
        Assert.True(DrumChartAnalysis.PeakBurstNps([.. notes.OrderBy(n => n.Time)]) >= 18.0);
    }

    [Fact]
    public void FastFillRate_IgnoresCymbalOnlyBursts()
    {
        // a fast cymbal/kick burst is NOT a fill (no snare/tom)
        var notes = Enumerable.Range(0, 16).Select(i =>
            new RoleNote(i / 16.0, i % 2 == 0 ? DrumRole.Kick : DrumRole.HiHat, 0u)).ToList();
        Assert.True(DrumChartAnalysis.FastFillRate(notes) < 6.0);
    }

    [Fact]
    public void FastFillRate_CountsSnareTomBursts()
    {
        var notes = Enumerable.Range(0, 16).Select(i =>
            new RoleNote(i / 16.0, i % 2 == 0 ? DrumRole.Snare : DrumRole.Tom, 0u)).ToList();
        Assert.True(DrumChartAnalysis.FastFillRate(notes) >= 12.0);
    }

    [Fact]
    public void ShortestTransitionGap_ReflectsFastestSpacing()
    {
        var notes = Enumerable.Range(0, 40).Select(i => new RoleNote(i / 20.0, DrumRole.Snare, 0u)).ToList(); // 0.05s gaps
        Assert.True(Math.Abs(DrumChartAnalysis.ShortestTransitionGap(notes) - 0.05) <= 0.01);
    }
}
