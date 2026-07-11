using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Progression;

public class PlayerProfileTests
{
    private static PlayerProfile Profile(int level)
        => new(new Dictionary<Attribute, AttributeState>
        {
            [Attribute.Strength] = new(1000, level),
            [Attribute.Endurance] = new(1000, level),
            [Attribute.Technique] = new(1000, level),
            [Attribute.Agility] = new(1000, level),
            [Attribute.Dexterity] = new(1000, level),
        });

    [Fact]
    public void ScoreIsTheSumOfLevels()
        => Assert.Equal(25.0, Profile(5).Score);

    [Fact]
    public void AllTensIsLegendweaver()
    {
        PlayerProfile maxed = Profile(10);
        Assert.Equal(50.0, maxed.Score);
        Assert.Equal(PlayerClass.Legendweaver, maxed.Class);
    }

    [Fact]
    public void MissingAxisReadsZero()
    {
        var empty = new PlayerProfile(new Dictionary<Attribute, AttributeState>());
        Assert.Equal(AttributeState.Zero, empty[Attribute.Strength]);
        Assert.Equal(0.0, empty.Score);
        Assert.Equal(PlayerClass.Busker, empty.Class);
    }
}
