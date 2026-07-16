using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Progression;

public class RewardProjectionTests
{
    private static AttributeProfile Song(double strength)
        => new(new Dictionary<Attribute, double> { [Attribute.Strength] = strength });

    private static Dictionary<Attribute, int> Levels(int all)
        => new()
        {
            [Attribute.Strength] = all,
            [Attribute.Endurance] = all,
            [Attribute.Technique] = all,
            [Attribute.Agility] = all,
            [Attribute.Dexterity] = all,
        };

    [Fact]
    public void MatchesFormulaAtCleanClear()
    {
        AttributeProfile song = Song(5.0);
        Dictionary<Attribute, int> levels = Levels(3);
        Dictionary<Attribute, double> expected = AttributeXpFormula.Award(
            song, new PerformanceFacts(1.0, false, Stars: 0, NotesHit: 0, NotesMissed: 0, Difficulty.Expert), levels);

        IReadOnlyDictionary<Attribute, double> actual = RewardProjection.ForCleanClear(song, levels);

        Assert.Equal(expected[Attribute.Strength], actual[Attribute.Strength], 6);
    }

    [Fact]
    public void HigherDemandProjectsMoreXp()
    {
        Dictionary<Attribute, int> levels = Levels(0);
        double low = RewardProjection.ForCleanClear(Song(2.0), levels)[Attribute.Strength];
        double high = RewardProjection.ForCleanClear(Song(8.0), levels)[Attribute.Strength];
        Assert.True(high > low);
    }
}
