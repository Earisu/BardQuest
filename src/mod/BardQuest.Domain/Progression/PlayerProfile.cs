using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Domain.Progression;

/// <summary>A player's derived character sheet for one quest: per-axis XP + level (0–10), the 0–50
/// aggregate (sum of the five levels — comparable to a chart's RankDerivation score), and the derived
/// (class, subrank). Purely derived from the quest's completed songs; never persisted.</summary>
public sealed class PlayerProfile
{
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

    public AttributeState this[Attribute a]
        => Axes.TryGetValue(a, out AttributeState? state) ? state : AttributeState.Zero;
}
