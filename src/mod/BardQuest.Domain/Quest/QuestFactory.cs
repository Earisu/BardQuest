using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

using YARG.Core;

namespace BardQuest.Domain.Quest;

/// <summary>Creates a fresh quest (the New-Quest onboarding entry point): no links, Busker-band working
/// set delivered from the matcher at score 0, no boss yet.</summary>
public static class QuestFactory
{
    public static Quest Create(
        Guid profileId, Instrument instrument, Difficulty difficulty, QuestPace pace,
        RatedLibrary library, DateTime createdAt)
    {
        DeliveryWindow window = MonsterMatcher.Window(playerScore: 0, band: PlayerClass.Busker);
        IReadOnlyList<string> set = MonsterMatcher.WorkingSet(
            library, window, MonsterMatcher.WorkingSetSize, new HashSet<string>());

        return new Quest(
            Guid.NewGuid(), profileId, instrument, difficulty, pace, createdAt,
            [], new DeliveryState(RerunCount: 0, WorkingSet: set, BossHash: null));
    }
}
