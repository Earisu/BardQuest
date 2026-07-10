using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalysisPrecisionTests
{
    private const uint Res = 480;

    private static List<RoleNote> AtTicks(IEnumerable<uint> ticks)
        => [.. ticks.Select(t => new RoleNote(t / 960.0, DrumRole.Snare, t))];

    [Fact]
    public void SyncopationFraction_AllOnBeat_IsZero()
    {
        List<RoleNote> notes = AtTicks(Enumerable.Range(0, 8).Select(i => (uint)(i * Res))); // every quarter
        Assert.Equal(0.0, DrumChartAnalysis.SyncopationFraction(notes, Res), 6);
    }

    [Fact]
    public void SyncopationFraction_AllOffBeatSixteenths_IsHigh()
    {
        // notes on the &-of e/a (quarter-tick offset Res/4 and 3Res/4) — off strong positions
        List<RoleNote> notes = AtTicks(Enumerable.Range(0, 8).SelectMany(i =>
            new[] { (uint)((i * Res) + (Res / 4)), (uint)((i * Res) + (3 * Res / 4)) }));
        Assert.True(DrumChartAnalysis.SyncopationFraction(notes, Res) > 0.9);
    }

    [Fact]
    public void ZeroResolution_IsZero()
    {
        List<RoleNote> notes = AtTicks([0, 120, 240]);
        Assert.Equal(0.0, DrumChartAnalysis.SyncopationFraction(notes, 0));
    }
}
