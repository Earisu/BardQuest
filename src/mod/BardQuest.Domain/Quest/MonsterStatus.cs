// src/mod/BardQuest.Domain/Quest/MonsterStatus.cs
using BardQuest.Domain.Ratings;

namespace BardQuest.Domain.Quest;

/// <summary>A delivered monster's live status for the UI: its profile, RankScore, whether it has been
/// defeated (a linked clear at the applicable bar), and its <see cref="MonsterType"/> (Regular, the
/// highlighted Elite mini-boss, the exclusive Boss, or a reserved Rare).</summary>
public sealed record MonsterStatus(
    string Hash, AttributeProfile Profile, double Sum, bool Defeated, MonsterType Type);
