// src/mod/BardQuest.Mod/Quest/QuestController.cs
extern alias yargpkg;

using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Domain.Ratings;

using YARG.Core;   // Instrument, Difficulty (domain-side, vendored)
using YARG.Player; // PlayerContainer, YargPlayer (Assembly-CSharp)

using DomainQuest = BardQuest.Domain.Quest.Quest;
using RtYargProfile = yargpkg::YARG.Core.Game.YargProfile;

namespace BardQuest.Mod.Quest;

// Orchestrates the read/launch side of the quest UI: active-profile discovery, view resolution, quest
// creation, and difficulty-aligned launching. All game/IO access lives here so the UITK screens stay
// presentation-only.
//
// Two YARG.Core assemblies, two copies of the same enums: the Domain Quest record's Instrument/Difficulty
// are the vendored YARG.Core.* (default alias), while YargProfile.CurrentInstrument/CurrentDifficulty (via
// Assembly-CSharp's PlayerContainer/YargPlayer) are the runtime yargpkg::YARG.Core.* — a distinct type.
// They are the same enum vendored twice (confirmed byte-identical: ProDrums=21, Expert=4, etc.), so a
// straight (int) round-trip bridges them.
public sealed class QuestController(ScoreSource scores, QuestLauncher launcher)
{
    public DomainQuest? ActiveQuest { get; private set; }

    // The primary configured player's profile; null if the user has no YARG player set up.
    public RtYargProfile? ActiveProfile()
    {
        foreach (YargPlayer p in PlayerContainer.Players)
        {
            return p.Profile;
        }

        return null;
    }

    public IReadOnlyList<DomainQuest> Quests()
    {
        RtYargProfile? profile = ActiveProfile();
        return profile == null ? [] : QuestStore.Load(profile.Id);
    }

    public ActiveQuestView Resolve(DomainQuest quest)
        => QuestEngine.Resolve(quest, LibraryFor(quest), scores);

    public DomainQuest Create(QuestPace pace, Difficulty difficulty)
    {
        RtYargProfile profile = ActiveProfile()
            ?? throw new InvalidOperationException("No active YARG profile to create a quest for.");
        var instrument = (Instrument)(int)profile.CurrentInstrument;
        var stub = new DomainQuest(
            Guid.Empty, profile.Id, instrument, difficulty, pace, DateTime.UtcNow, [], null);
        DomainQuest quest = QuestFactory.Create(
            profile.Id, instrument, difficulty, pace, LibraryFor(stub), DateTime.UtcNow);

        IReadOnlyList<DomainQuest> all = [.. QuestStore.Load(profile.Id), quest];
        QuestStore.Save(all);
        return quest;
    }

    public void Launch(DomainQuest quest, string songHashHex)
    {
        // Align the active player to the quest so the play is scored under the quest's (instrument,
        // difficulty). Direct-launch bypasses DifficultySelectMenu, so this is what makes it correlate.
        foreach (YargPlayer p in PlayerContainer.Players)
        {
            if (p.Profile.Id == quest.ProfileId)
            {
                p.Profile.CurrentInstrument = (yargpkg::YARG.Core.Instrument)(int)quest.Instrument;
                p.Profile.CurrentDifficulty = (yargpkg::YARG.Core.Difficulty)(int)quest.Difficulty;
                break;
            }
        }

        ActiveQuest = quest;
        launcher.Launch(quest, songHashHex);
    }

    // Keeps ActiveQuest current after a record-on-return rewrite, so a subsequent play in the same session
    // (without relaunching through Launch) still correlates against the up-to-date quest.
    public void Adopt(DomainQuest quest) => ActiveQuest = quest;

    // The rated library for a quest's (instrument, difficulty), built from the cache on disk. Internal, not
    // private: BardQuestManager's record-on-return path builds the same library and reuses this rather than
    // keeping a second copy.
    internal static RatedLibrary LibraryFor(DomainQuest quest)
    {
        Dictionary<string, List<ChartMetrics>> cache = Scan.RatingCacheFile.Load();
        var view = new Dictionary<string, IReadOnlyList<ChartMetrics>>(cache.Count);
        foreach (KeyValuePair<string, List<ChartMetrics>> kv in cache)
        {
            view[kv.Key] = kv.Value;
        }

        return new RatedLibrary(view, quest.Instrument, quest.Difficulty);
    }
}
