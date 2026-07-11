// src/mod/BardQuest.Domain/Quest/MonsterStatus.cs
using BardQuest.Domain.Ratings;

namespace BardQuest.Domain.Quest;

/// <summary>A delivered monster's live status for the UI: its profile, RankScore, whether it has been
/// defeated (a linked clear at the applicable bar), and whether it is the highlighted mini-boss or the
/// exclusive class boss.</summary>
public sealed record MonsterStatus(
    string Hash, AttributeProfile Profile, double Sum, bool Defeated, bool IsMiniBoss, bool IsBoss);
