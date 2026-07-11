namespace BardQuest.Domain.Ratings;

/// <summary>The five RPG skill axes a chart can demand. Shared across instrument families;
/// each family measures its own raw metrics but derives these same attributes.</summary>
public enum Attribute
{
    Strength,
    Endurance,
    Technique,
    Agility,
    Dexterity,
}
