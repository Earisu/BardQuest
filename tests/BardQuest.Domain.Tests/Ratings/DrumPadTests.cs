using BardQuest.Domain.Ratings;

using Xunit;

using YargPad = YARG.Core.Chart.FourLaneDrumPad;

namespace BardQuest.Domain.Tests.Ratings;

public class DrumPadTests
{
    // The Mod maps YARG's FourLaneDrumPad to our DrumPad with a plain (int) cast, so the ordinals
    // MUST match. This test locks that contract against the vendored YARG.Core.
    [Theory]
    [InlineData(DrumPad.Kick, YargPad.Kick)]
    [InlineData(DrumPad.RedDrum, YargPad.RedDrum)]
    [InlineData(DrumPad.YellowDrum, YargPad.YellowDrum)]
    [InlineData(DrumPad.BlueDrum, YargPad.BlueDrum)]
    [InlineData(DrumPad.GreenDrum, YargPad.GreenDrum)]
    [InlineData(DrumPad.YellowCymbal, YargPad.YellowCymbal)]
    [InlineData(DrumPad.BlueCymbal, YargPad.BlueCymbal)]
    [InlineData(DrumPad.GreenCymbal, YargPad.GreenCymbal)]
    public void DrumPad_OrdinalsMatchYargFourLaneDrumPad(DrumPad ours, YargPad theirs)
        => Assert.Equal((int)theirs, (int)ours);
}
