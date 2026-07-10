using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalysisDexterityTests
{
    [Fact]
    public void KitPieceEntropy_ConcentratedOnOnePiece_IsZero()
    {
        // A single-tom groove — all hits on one lane — is no ranging around the kit, however many notes.
        var notes = Enumerable.Range(0, 16).Select(i => new RoleNote(i * 0.25, DrumRole.Tom, 0u, 2)).ToList();
        Assert.Equal(0.0, DrumChartAnalysis.KitPieceEntropy(notes), 6);
    }

    [Fact]
    public void KitPieceEntropy_ExcludesKick()
    {
        // Kick is a foot on a fixed pedal, not hand navigation: snare + kick is still one hand piece.
        var notes = Enumerable.Range(0, 16)
            .Select(i => i % 2 == 0 ? new RoleNote(i * 0.25, DrumRole.Snare, 0u, 1) : new RoleNote(i * 0.25, DrumRole.Kick, 0u, 0))
            .ToList();
        Assert.Equal(0.0, DrumChartAnalysis.KitPieceEntropy(notes), 6);
    }

    [Fact]
    public void KitPieceEntropy_SeparatesTomsCollapsedByRole()
    {
        // Two "Tom" notes on different lanes are different pieces even though DrumRole calls both Tom:
        // a sweep across three toms + cymbals must out-score pounding a single tom against the snare.
        var narrow = new List<RoleNote>
        {
            new(0, DrumRole.Snare, 0u, 1), new(1, DrumRole.Tom, 0u, 2),
            new(2, DrumRole.Snare, 0u, 1), new(3, DrumRole.Tom, 0u, 2),
        };
        var wide = new List<RoleNote>
        {
            new(0, DrumRole.Snare, 0u, 1), new(1, DrumRole.Tom, 0u, 2), new(2, DrumRole.Tom, 0u, 3),
            new(3, DrumRole.Tom, 0u, 4), new(4, DrumRole.Cymbal, 0u, 6), new(5, DrumRole.Cymbal, 0u, 7),
        };
        Assert.True(DrumChartAnalysis.KitPieceEntropy(wide) > DrumChartAnalysis.KitPieceEntropy(narrow));
    }
}
