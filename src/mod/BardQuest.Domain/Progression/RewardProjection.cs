using BardQuest.Domain.Ratings;

using YARG.Core;

using Attribute = BardQuest.Domain.Ratings.Attribute;

namespace BardQuest.Domain.Progression;

/// <summary>The per-axis XP a song would award on a clean full clear (100%, no full-combo kicker) at the
/// player's current levels — the encounter panel's "XP on clear" preview. Quality is unknown before a
/// play, so the preview fixes it to a clean clear; only the raw, already-calibrated formula values are
/// shown. The reference <see cref="PerformanceFacts"/> fields other than percent/FC are unused by
/// <see cref="AttributeXpFormula"/>, so they carry placeholder values.</summary>
public static class RewardProjection
{
    private static readonly PerformanceFacts CleanClear =
        new(Percent: 1.0, IsFc: false, Stars: 0, NotesHit: 0, NotesMissed: 0, Difficulty.Expert);

    public static IReadOnlyDictionary<Attribute, double> ForCleanClear(
        AttributeProfile song, IReadOnlyDictionary<Attribute, int> currentLevels)
        => AttributeXpFormula.Award(song, CleanClear, currentLevels);
}
