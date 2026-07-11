namespace BardQuest.Domain.Quest;

/// <summary>The currently-offered monsters — persisted only for UX stability (delivery has randomness,
/// so the player should see the same monsters across sessions). Nothing here ranks or gates: that is all
/// derived from the links. <see cref="BossHash"/> is set only during an exclusive class-boss phase.</summary>
public sealed record DeliveryState(int RerunCount, IReadOnlyList<string> WorkingSet, string? BossHash);
