using BardQuest.Domain.Ratings;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Domain.Progression;

/// <summary>Builds a quest's <see cref="PlayerProfile"/> from its completed songs — the subsystem
/// entry point. Folds the plays in chronological order (each scored against the level accrued from
/// earlier plays, because the gap penalty references your current level), so the profile is a pure
/// function of the date-ordered links + scores.db + rating cache: retune a constant, rebuild exactly,
/// nothing stored.</summary>
public static class PlayerProgress
{
    private static readonly Attribute[] Axes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    public static PlayerProfile Build(
        IReadOnlyList<(AttributeProfile Song, PerformanceFacts Performance)> completedInDateOrder, QuestPace pace)
    {
        var curve = LevelCurve.ForPace(pace);
        var xp = new Dictionary<Attribute, double>(Axes.Length);
        var level = new Dictionary<Attribute, int>(Axes.Length);
        foreach (Attribute axis in Axes)
        {
            xp[axis] = 0;
            level[axis] = 0;
        }

        foreach ((AttributeProfile song, PerformanceFacts perf) in completedInDateOrder)
        {
            Dictionary<Attribute, double> award = AttributeXpFormula.Award(song, perf, level);
            foreach (Attribute axis in Axes)
            {
                xp[axis] += award[axis];
                level[axis] = curve.LevelFor(xp[axis]);
            }
        }

        var axes = new Dictionary<Attribute, AttributeState>(Axes.Length);
        foreach (Attribute axis in Axes)
        {
            axes[axis] = new AttributeState(xp[axis], level[axis]);
        }

        return new PlayerProfile(axes);
    }
}
