using BardQuest.Domain.Progression;

using Xunit;

namespace BardQuest.Domain.Tests.Progression;

public class ClassDerivationTests
{
    [Fact]
    public void FloorIsBuskerFirstSubrank()
    {
        (PlayerClass cls, int subrank) = ClassDerivation.Derive(0);
        Assert.Equal(PlayerClass.Busker, cls);
        Assert.Equal(0, subrank);
    }

    [Fact]
    public void CeilingIsLegendweaverTopSubrank()
    {
        (PlayerClass cls, int subrank) = ClassDerivation.Derive(ClassDerivation.MaxScore);
        Assert.Equal(PlayerClass.Legendweaver, cls);
        Assert.Equal(ClassDerivation.SubranksPerClass - 1, subrank);
    }

    [Fact]
    public void ClassIsMonotonicInScore()
    {
        PlayerClass low = ClassDerivation.Derive(5).Class;
        PlayerClass mid = ClassDerivation.Derive(25).Class;
        PlayerClass high = ClassDerivation.Derive(45).Class;
        Assert.True(low < mid && mid < high);
    }

    [Fact]
    public void SubrankRisesWithinAClass()
    {
        // Two scores in the same class band should give non-decreasing subranks.
        (PlayerClass c1, int s1) = ClassDerivation.Derive(1);
        (PlayerClass c2, int s2) = ClassDerivation.Derive(11);
        Assert.Equal(c1, c2);          // both Busker (band 0..12)
        Assert.True(s2 > s1);
    }

    [Fact]
    public void ClampsOutOfRangeScores()
    {
        Assert.Equal(PlayerClass.Busker, ClassDerivation.Derive(-10).Class);
        Assert.Equal(PlayerClass.Legendweaver, ClassDerivation.Derive(999).Class);
    }
}
