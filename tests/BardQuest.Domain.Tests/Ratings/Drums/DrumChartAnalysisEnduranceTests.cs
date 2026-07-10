using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalysisEnduranceTests
{
    private static List<RoleNote> Kicks(int count, double rate, double start = 0)
        => [.. Enumerable.Range(0, count).Select(i => new RoleNote(start + (i / rate), DrumRole.Kick, 0u))];

    [Fact]
    public void KickDensity_CountsOnlyKicks()
    {
        var notes = Kicks(20, 4.0).Concat([new RoleNote(0.1, DrumRole.Snare, 0u)]).OrderBy(n => n.Time).ToList();
        Assert.Equal(4.0, DrumChartAnalysis.KickDensity(notes, 5.0), 1);
    }

    [Fact]
    public void LongestKickRun_CountsConsecutiveFastKicks()
    {
        var notes = new List<RoleNote>();
        notes.AddRange(Kicks(3, 1.0, 0.0));    // slow: gaps 1s > 0.30 -> runs of 1
        notes.AddRange(Kicks(10, 8.0, 10.0));  // fast: gaps 0.125s -> run of 10
        Assert.Equal(10, DrumChartAnalysis.LongestKickRun([.. notes.OrderBy(n => n.Time)], 0.30));
    }

    [Fact]
    public void FastestKickSpanNps_IsContinuous_NotWindowQuantised()
    {
        // A uniform 6.4 kicks/s stream must read ~6.4, not snap to a window multiple like 6 or 8.
        double nps = DrumChartAnalysis.FastestKickSpanNps(Kicks(16, 6.4));
        Assert.Equal(6.4, nps, 1);
    }

    [Fact]
    public void FastestKickSpanNps_FindsTheFastSpanInsideASlowChart()
    {
        var notes = Kicks(20, 2.0).Concat(Kicks(8, 7.5, 30.0)).OrderBy(n => n.Time).ToList();
        Assert.Equal(7.5, DrumChartAnalysis.FastestKickSpanNps(notes), 1);
    }

    [Fact]
    public void FastestKickSpanNps_NeedsAFullSpanOfKicks()
        => Assert.Equal(0.0, DrumChartAnalysis.FastestKickSpanNps(Kicks(DrumChartAnalysis.KickSpanNotes - 1, 8.0)), 6);
}
