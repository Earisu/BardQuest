using BardQuest.Domain.Progression;

namespace BardQuest.Domain.Quest;

/// <summary>The derived state of a quest at a moment: the honest XP <see cref="PlayerProfile"/>, how many
/// gate steps have been unlocked, the boss-clamped <see cref="EffectiveStep"/>
/// (= min(xpStanding, gatesUnlocked)), and whether Legendweaver has been reached.</summary>
public sealed record QuestState(PlayerProfile Profile, int GatesUnlocked, int EffectiveStep, bool IsComplete);
