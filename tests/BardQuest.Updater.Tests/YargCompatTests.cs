using BardQuest.Updater.Core.Compatibility;

using Xunit;

namespace BardQuest.Updater.Tests;

public class YargCompatTests
{
    [Theory]
    [InlineData("v0.15.0", "v0.15.0")]
    [InlineData("v0.15.0", "V0.15.0")]
    [InlineData("0.15.0", "v0.15.0")]
    [InlineData("b3642", "b3642")]
    [InlineData("  v0.15.0  ", "0.15.0")]
    public void Evaluate_Compatible_WhenNormalizedEqual(string modTarget, string installTag) =>
        Assert.Equal(Compatibility.Compatible, YargCompat.Evaluate(modTarget, installTag));

    [Theory]
    [InlineData("v0.15.0", "v0.16.0")]
    [InlineData("v0.15.0", "b3642")]
    [InlineData("b3641", "b3642")]
    public void Evaluate_Incompatible_WhenKnownAndDifferent(string modTarget, string installTag) =>
        Assert.Equal(Compatibility.Incompatible, YargCompat.Evaluate(modTarget, installTag));

    [Theory]
    [InlineData(null, "v0.15.0")]
    [InlineData("v0.15.0", null)]
    [InlineData("", "v0.15.0")]
    [InlineData("v0.15.0", "   ")]
    [InlineData(null, null)]
    public void Evaluate_Unverified_WhenEitherUnknown(string? modTarget, string? installTag) =>
        Assert.Equal(Compatibility.Unverified, YargCompat.Evaluate(modTarget, installTag));
}
