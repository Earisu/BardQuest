using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class AttributeProfileTests
{
    private static AttributeProfile Profile(double s, double e, double t, double a, double d)
        => new(new Dictionary<Attribute, double>
        {
            [Attribute.Strength] = s,
            [Attribute.Endurance] = e,
            [Attribute.Technique] = t,
            [Attribute.Agility] = a,
            [Attribute.Dexterity] = d,
        });

    [Fact]
    public void Indexer_ReturnsScore()
        => Assert.Equal(9.0, Profile(9, 10, 6, 8, 7)[Attribute.Strength]);

    [Fact]
    public void Sum_AddsAllFive()
        => Assert.Equal(40.0, Profile(9, 10, 6, 8, 7).Sum(), 6);

    [Fact]
    public void MissingAttribute_ReadsZero()
        => Assert.Equal(0.0, new AttributeProfile(new Dictionary<Attribute, double>())[Attribute.Dexterity]);

    [Fact]
    public void Threat_IsHighestSingleAttribute()
        => Assert.Equal(10.0, Profile(9, 10, 6, 8, 7).Threat(), 6);
}
