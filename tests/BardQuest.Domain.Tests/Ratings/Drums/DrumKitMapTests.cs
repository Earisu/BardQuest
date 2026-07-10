using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumKitMapTests
{
    [Theory]
    [InlineData(0, DrumRole.Kick)]
    [InlineData(1, DrumRole.Snare)]
    [InlineData(2, DrumRole.Tom)]
    [InlineData(3, DrumRole.Tom)]
    [InlineData(4, DrumRole.Tom)]
    [InlineData(5, DrumRole.HiHat)]
    [InlineData(6, DrumRole.Cymbal)]
    [InlineData(7, DrumRole.Cymbal)]
    public void ProFourLane_MapsPadOrdinalToRole(int lane, DrumRole expected)
        => Assert.Equal(expected, DrumKitMap.ProFourLane.Map(lane));

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(9)]
    public void ProFourLane_DropsOutOfVocabularyLanes(int lane)
        => Assert.Null(DrumKitMap.ProFourLane.Map(lane));

    [Fact]
    public void IsCymbalFamily_CountsCymbalAndHiHat()
    {
        Assert.True(DrumKitMap.IsCymbalFamily(DrumRole.Cymbal));
        Assert.True(DrumKitMap.IsCymbalFamily(DrumRole.HiHat));
        Assert.False(DrumKitMap.IsCymbalFamily(DrumRole.Tom));
    }
}
