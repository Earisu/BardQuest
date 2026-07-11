using BardQuest.Domain.Ratings;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Domain.Progression;

/// <summary>A player's derived character sheet for one quest: per-axis XP + level (0–10), the 0–50
/// aggregate (sum of the five levels — comparable to a chart's rank score, see
/// <see cref="AttributeProfile.ToRank"/>), and the derived (class, subrank). Purely derived from the
/// quest's completed songs; never persisted.</summary>
public sealed class PlayerProfile
{
    private static readonly Attribute[] AllAxes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    public IReadOnlyDictionary<Attribute, AttributeState> Axes { get; }

    /// <summary>Sum of the five levels, 0–50 — the same axis charts are ranked on.</summary>
    public double Score { get; }

    public PlayerClass Class { get; }

    /// <summary>0..2, rendered I/II/III.</summary>
    public int Subrank { get; }

    public PlayerProfile(IReadOnlyDictionary<Attribute, AttributeState> axes)
    {
        Axes = axes;
        double score = 0;
        foreach (AttributeState state in axes.Values)
        {
            score += state.Level;
        }

        Score = score;
        (Class, Subrank) = ClassDerivation.Derive(score);
    }

    /// <summary>Builds the character sheet from a quest's completed songs — the subsystem entry point.
    /// Folds the plays in chronological order (each scored against the level accrued from earlier plays,
    /// because the gap penalty references your current level), so the profile is a pure function of the
    /// date-ordered links + scores.db + rating cache: retune a constant, rebuild exactly, nothing stored.</summary>
    public static PlayerProfile FromCompletedSongs(
        IReadOnlyList<(AttributeProfile Song, PerformanceFacts Performance)> completedInDateOrder, QuestPace pace)
    {
        var curve = LevelCurve.ForPace(pace);
        var xp = new Dictionary<Attribute, double>(AllAxes.Length);
        var level = new Dictionary<Attribute, int>(AllAxes.Length);
        foreach (Attribute axis in AllAxes)
        {
            xp[axis] = 0;
            level[axis] = 0;
        }

        foreach ((AttributeProfile song, PerformanceFacts perf) in completedInDateOrder)
        {
            Dictionary<Attribute, double> award = AttributeXpFormula.Award(song, perf, level);
            foreach (Attribute axis in AllAxes)
            {
                xp[axis] += award[axis];
                level[axis] = curve.LevelFor(xp[axis]);
            }
        }

        var axes = new Dictionary<Attribute, AttributeState>(AllAxes.Length);
        foreach (Attribute axis in AllAxes)
        {
            axes[axis] = new AttributeState(xp[axis], level[axis]);
        }

        return new PlayerProfile(axes);
    }

    public AttributeState this[Attribute a]
        => Axes.TryGetValue(a, out AttributeState? state) ? state : AttributeState.Zero;
}
