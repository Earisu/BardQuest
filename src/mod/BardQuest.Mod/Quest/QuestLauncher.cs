// src/mod/BardQuest.Mod/Quest/QuestLauncher.cs
extern alias yargpkg;

using BardQuest.Domain.Quest;

using YARG;               // GlobalVariables, SceneIndex
using YARG.Core;          // Instrument, Difficulty (domain-side)

using DomainQuest = BardQuest.Domain.Quest.Quest;
using RtSongEntry = yargpkg::YARG.Core.Song.SongEntry;

namespace BardQuest.Mod.Quest;

// Direct-launch a quest song into YARG's native play flow and remember enough to correlate the resulting
// scores.db record on return. Capturing max(GameRecord.Id) BEFORE the play makes the post-play query
// unambiguous (rowid monotonic) with no 2nd Cecil seam.
//
// Mirrors YARG's own direct-launch precedent (YARG.Menu.History.ViewType.LoadIntoReplay and
// PauseMenuManager.Restart/Skip): set GlobalVariables.State.CurrentSong then LoadScene(Gameplay)
// directly, bypassing DifficultySelectMenu. Because DifficultySelectMenu is skipped, the play is scored
// under whichever Instrument/Difficulty the ACTIVE player's YargProfile.CurrentInstrument/
// CurrentDifficulty already carry (YARG.Gameplay.GameManager.RecordScores reads profile.Current*, not
// anything QuestLauncher sets) — so a caller must only launch a quest whose (Instrument, Difficulty) match
// the active player's profile, or the resulting play will not correlate (silently, correctly, as no
// credit).
public sealed class QuestLauncher(ScoreSource scores)
{
    public PendingLaunch? Pending { get; private set; }

    public readonly record struct PendingLaunch(
        Guid QuestId, string SongHashHex, Guid ProfileId, Instrument Instrument, Difficulty Difficulty, int BaselineId);

    public void Launch(DomainQuest quest, string songHashHex)
    {
        RtSongEntry? entry = SongCatalog.ByHash(songHashHex);
        if (entry == null)
        {
            ModLog.Error($"QuestLauncher: song {songHashHex} not in library.");
            return;
        }

        int baseline = scores.MaxGameRecordId();
        Pending = new PendingLaunch(quest.Id, songHashHex, quest.ProfileId, quest.Instrument, quest.Difficulty, baseline);

        GlobalVariables.State.CurrentSong = entry;
        GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
    }

    // Correlate the just-finished play (called on return from Gameplay). Null if nothing new was recorded
    // (quit/unfinished/invalid) — no credit, correctly.
    public ProvenanceLink? Correlate()
    {
        if (Pending is not PendingLaunch p)
        {
            return null;
        }

        Pending = null;
        PlayRecord? rec = scores.NewestSince(p.BaselineId, p.SongHashHex, p.ProfileId, p.Instrument, p.Difficulty);
        return rec == null ? null : new ProvenanceLink(rec.PlayerScoreRecordId, rec.SongHashHex, rec.PlayedAt);
    }

}
