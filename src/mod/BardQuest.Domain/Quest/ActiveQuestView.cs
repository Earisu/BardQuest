// src/mod/BardQuest.Domain/Quest/ActiveQuestView.cs
using BardQuest.Domain.Progression;

namespace BardQuest.Domain.Quest;

/// <summary>The read model the UI renders — the honest XP profile, the boss-clamped effective class and
/// subrank, whether the player is at an exclusive class-boss phase, the working set with live statuses,
/// and the boss (when at one). Purely derived; never persisted.</summary>
public sealed record ActiveQuestView(
    PlayerProfile Profile,
    PlayerClass Class,
    int Subrank,
    int EffectiveStep,
    bool IsComplete,
    bool AtClassBoss,
    IReadOnlyList<MonsterStatus> WorkingSet,
    MonsterStatus? Boss);
