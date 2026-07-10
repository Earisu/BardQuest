using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class AttributeProfileTests
{
    private static AttributeProfile Profile(double s, double e, double t, double a, double p, double d)
        => new(new Dictionary<Attribute, double>
        {
            [Attribute.Strength] = s,
            [Attribute.Endurance] = e,
            [Attribute.Technique] = t,
            [Attribute.Agility] = a,
            [Attribute.Precision] = p,
            [Attribute.Dexterity] = d,
        });

    [Fact]
    public void Indexer_ReturnsScore()
        => Assert.Equal(9.0, Profile(9, 10, 6, 8, 5, 7)[Attribute.Strength]);

    [Fact]
    public void Sum_AddsAllSix()
        => Assert.Equal(45.0, Profile(9, 10, 6, 8, 5, 7).Sum(), 6);

    [Fact]
    public void MissingAttribute_ReadsZero()
        => Assert.Equal(0.0, new AttributeProfile(new Dictionary<Attribute, double>())[Attribute.Precision]);

    [Fact]
    public void Threat_IsHighestSingleAttribute()
        => Assert.Equal(10.0, Profile(9, 10, 6, 8, 5, 7).Threat(), 6);
}
