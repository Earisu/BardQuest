using BardQuest.Domain.Progression;

using YARG.Core;

namespace BardQuest.Domain.Quest;

/// <summary>A quest save — the irreducible persisted state. Scoped to one YARG profile, instrument and
/// difficulty. XP, levels, class, ladder position and completion are NOT stored: they are a pure
/// function of <see cref="Links"/> (see QuestEngine). The goal is always Legendweaver; <see cref="Pace"/>
/// is the only length knob.</summary>
public sealed record Quest(
    Guid Id,
    Guid ProfileId,
    Instrument Instrument,
    Difficulty Difficulty,
    QuestPace Pace,
    DateTime CreatedAt,
    IReadOnlyList<ProvenanceLink> Links,
    DeliveryState Delivery);
