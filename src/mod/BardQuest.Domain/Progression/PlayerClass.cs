namespace BardQuest.Domain.Progression;

/// <summary>The bard class ladder — the player's overall standing, the counterpart to a chart's Rank
/// on the same 0–50 five-axis-sum axis. Instrument-agnostic. Each class splits into three subranks
/// (I/II/III), carried by frame art in the UI. Display names/colors are a presentation concern.</summary>
public enum PlayerClass
{
    Busker,
    Minstrel,
    Troubadour,
    Bard,
    Skald,
    Legendweaver,
}
