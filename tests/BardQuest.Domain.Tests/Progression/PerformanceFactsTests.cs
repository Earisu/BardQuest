using BardQuest.Domain.Progression;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Progression;

public class PerformanceFactsTests
{
    [Fact]
    public void CarriesTheScoreRowFields()
    {
        var p = new PerformanceFacts(0.94, IsFc: false, Stars: 5, NotesHit: 470, NotesMissed: 30, Difficulty.Expert);

        Assert.Equal(0.94, p.Percent);
        Assert.False(p.IsFc);
        Assert.Equal(5, p.Stars);
        Assert.Equal(470, p.NotesHit);
        Assert.Equal(30, p.NotesMissed);
        Assert.Equal(Difficulty.Expert, p.Difficulty);
    }
}
