using BardQuest.Domain.Ratings;
using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalysisMeasureTests
{
    [Fact]
    public void Measure_PopulatesAllFields_FromASimpleGroove()
    {
        var notes = new List<RoleNote>();
        for (int i = 0; i < 16; i++)
        {
            notes.Add(new RoleNote(i * 0.25, DrumRole.HiHat, (uint)(i * 240)));
            if (i % 2 == 0)
            {
                notes.Add(new RoleNote(i * 0.25, DrumRole.Kick, (uint)(i * 240)));
            }
            else
            {
                notes.Add(new RoleNote(i * 0.25, DrumRole.Snare, (uint)(i * 240)));
            }
        }

        notes = [.. notes.OrderBy(n => n.Time)];
        var sync = new SyncInfo(480, [new(0.0, 4, 4)]);
        DrumRawMetrics m = DrumChartAnalysis.Measure(notes, durationSeconds: 4.0, sync);

        Assert.True(m.AvgNps > 0);
        Assert.True(m.KickDensity > 0);
        Assert.True(m.PeakBurstNps > 0);
        Assert.Equal(0.0, m.OddMeterFraction, 6); // all 4/4
    }

    [Fact]
    public void Measure_PatternVariety_LowForLoop_HighForVaried()
    {
        var sync = new SyncInfo(480, [new(0.0, 4, 4)]); // 480 ticks/quarter -> 1920 ticks/bar
        var loop = new List<RoleNote>();
        for (int bar = 0; bar < 4; bar++)
        {
            uint b = (uint)(bar * 1920);
            loop.Add(new RoleNote(bar * 2.0, DrumRole.Kick, b));
            loop.Add(new RoleNote((bar * 2.0) + 0.5, DrumRole.Snare, b + 960));
        }

        double looped = DrumChartAnalysis.Measure(loop, 8.0, sync).PatternVariety;
        Assert.True(looped < 0.5); // 4 identical bars -> 1 distinct / 4

        var varied = new List<RoleNote>();
        for (int bar = 0; bar < 4; bar++)
        {
            uint b = (uint)(bar * 1920);
            for (int k = 0; k <= bar; k++)
            {
                varied.Add(new RoleNote((bar * 2.0) + (k * 0.1), DrumRole.Tom, (uint)(b + (k * 120))));
            }
        }

        Assert.True(DrumChartAnalysis.Measure(varied, 8.0, sync).PatternVariety > looped);
    }

    [Fact]
    public void Measure_EmptyNotes_IsAllZeroButDoesNotThrow()
    {
        DrumRawMetrics m = DrumChartAnalysis.Measure([], 0.0,
            new SyncInfo(480, []));
        Assert.Equal(0.0, m.PeakNps);
        Assert.Equal(0, m.LongestKickRun);
    }
}
