using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

using Xunit;
using YARG.Core;

namespace BardQuest.Domain.Tests.Progression;

public class AttributeXpFormulaTests
{
    private static PerformanceFacts Perf(double percent, bool fc = false)
        => new(percent, fc, Stars: 5, NotesHit: 100, NotesMissed: 0, Difficulty.Expert);

    private static AttributeProfile Profile(double strength, double technique)
        => new(new Dictionary<Attribute, double>
        {
            [Attribute.Strength] = strength,
            [Attribute.Technique] = technique,
        });

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
    public void QualityIsZeroBelowClearThreshold()
        => Assert.Equal(0.0, AttributeXpFormula.QualityScale(Perf(0.80)));

    [Fact]
    public void QualityRisesWithPercentAndTopsAtFullNoFc()
    {
        Assert.Equal(1.0, AttributeXpFormula.QualityScale(Perf(1.0)), 6);
        Assert.True(AttributeXpFormula.QualityScale(Perf(0.95)) > AttributeXpFormula.QualityScale(Perf(0.90)));
    }

    [Fact]
    public void FullComboAddsBonus()
        => Assert.True(AttributeXpFormula.QualityScale(Perf(1.0, fc: true)) > AttributeXpFormula.QualityScale(Perf(1.0)));

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 0.7)]
    [InlineData(2, 0.49)]
    [InlineData(3, 0.0)]
    [InlineData(5, 0.0)]
    public void GapPenaltyDiminishesToZeroAtCutoff(double over, double expected)
        => Assert.Equal(expected, AttributeXpFormula.GapPenalty(over), 6);

    [Fact]
    public void AwardScalesWithDemandAndUsesXpScale()
    {
        Dictionary<Attribute, double> award = AttributeXpFormula.Award(Profile(strength: 6, technique: 2), Perf(1.0), Levels(0));
        // demand 6 × quality 1.0 × gap 1.0 × XpScale 100 = 600; Strength beats Technique (2 demand).
        Assert.Equal(600.0, award[Attribute.Strength], 6);
        Assert.True(award[Attribute.Strength] > award[Attribute.Technique]);
    }

    [Fact]
    public void OutleveledAxisEarnsNothing()
    {
        // Song demands Strength 3; player is Strength 6 (over = 3 ≥ Cutoff) → zero.
        Dictionary<Attribute, double> award = AttributeXpFormula.Award(Profile(strength: 3, technique: 0), Perf(1.0), Levels(6));
        Assert.Equal(0.0, award[Attribute.Strength], 6);
    }
}
