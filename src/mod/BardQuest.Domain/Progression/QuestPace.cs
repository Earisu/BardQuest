namespace BardQuest.Domain.Progression;

/// <summary>How fast a quest levels attributes. Scales the level curve's cost (never the awards), so
/// it changes songs-per-level, not the XP a given play grants. Chosen once at quest creation.</summary>
public enum QuestPace
{
    Sprint,
    Journey,
    Odyssey,
}
