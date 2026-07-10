using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalysisStrengthTests
{
    private static List<RoleNote> Snare(int count, double rate, double start = 0)
        => [.. Enumerable.Range(0, count).Select(i => new RoleNote(start + (i / rate), DrumRole.Snare, 0u))];

    [Fact]
    public void AvgNps_IsCountOverDuration()
        => Assert.Equal(5.0, DrumChartAnalysis.AvgNps(Snare(50, 5.0), 10.0), 6);

    [Fact]
    public void PeakNps_SteadyStream_IsAboutRate()
        => Assert.True(Math.Abs(DrumChartAnalysis.PeakNps(Snare(80, 8.0)) - 8.0) <= 0.5);

    [Fact]
    public void LongestDenseSection_MeasuresTheDenseSpanOnly()
    {
        var notes = new List<RoleNote>();
        notes.AddRange(Snare(6, 2.0, 0.0));    // ~0..2.5s slow (below threshold)
        notes.AddRange(Snare(40, 10.0, 5.0));  // 5..~9s dense (>=8/s)
        notes = [.. notes.OrderBy(n => n.Time)];
        double dense = DrumChartAnalysis.LongestDenseSectionSeconds(notes, 8.0);
        Assert.True(dense >= 3.0, $"dense span {dense}");
    }

    [Fact]
    public void LongestDenseSection_NoDensePassage_IsZero()
        => Assert.Equal(0.0, DrumChartAnalysis.LongestDenseSectionSeconds(Snare(20, 2.0), 8.0));
}
