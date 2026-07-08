namespace BardQuest.Domain.Ratings;

/// <summary>
/// Per-(chart) difficulty signals derived purely from a single charted difficulty's note hits.
/// PeakNps/AvgNps are notes-per-second; the four technique fields are graded intensities in [0,1].
/// Turned into a within-tier sub-score by <see cref="ChartRatingCalculator.SubScore(ChartDifficultyProfile)"/>.
/// </summary>
public readonly record struct ChartDifficultyProfile(
    double PeakNps,
    double AvgNps,
    double DoubleBass,
    double BlastBeat,
    double Independence,
    double FastFill);
