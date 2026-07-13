// src/mod/BardQuest.Domain/Quest/MonsterType.cs
namespace BardQuest.Domain.Quest;

/// <summary>What kind of encounter a delivered monster is. <see cref="Elite"/> is the highlighted
/// breakthrough mini-boss (a same-rank song whose clear at the accuracy bar breaks a subrank);
/// <see cref="Boss"/> is the exclusive class boss; <see cref="Rare"/> is reserved for a future
/// chance-encounter mechanic (delivery/objective/XP not yet implemented — nothing produces it).</summary>
public enum MonsterType
{
    Regular,
    Elite,
    Boss,
    Rare,
}
