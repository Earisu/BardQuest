namespace BardQuest.Domain.Progression;

/// <summary>One axis's derived progression: cumulative XP and the level (0–10) it buys.</summary>
public sealed record AttributeState(double Xp, int Level)
{
    public static AttributeState Zero { get; } = new(0, 0);
}
