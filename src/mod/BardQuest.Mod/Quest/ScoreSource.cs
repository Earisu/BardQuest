// src/mod/BardQuest.Mod/Quest/ScoreSource.cs
using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;

using SQLite; // YARG's bundled sqlite-net — compiled straight into Assembly-CSharp (no separate plugin
              // DLL ships it), so this `using` resolves via the existing Assembly-CSharp reference.

using YARG.Core;
using YARG.Scores; // ScoreContainer.ScoreDirectory

namespace BardQuest.Mod.Quest;

// Read-only view over YARG's scores.db. Opens its OWN connection (Mode=ReadOnly) — never writes. Resolves
// provenance links to PerformanceFacts, and serves the capture queries (max rowid baseline + newest
// record after a baseline) used by QuestLauncher. All queries filter out replays (PlayerScores.IsReplay).
public sealed class ScoreSource : IScoreSource, IDisposable
{
    private readonly SQLiteConnection _db;

    public ScoreSource()
    {
        string path = Path.Combine(ScoreContainer.ScoreDirectory, "scores.db");
        _db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly, storeDateTimeAsTicks: true);
    }

    // Projection rows (sqlite-net maps result columns onto these by name; they mirror the
    // PlayerScores/GameRecords join). Kept private/mutable-POCO — sqlite-net's Query<T> needs a
    // parameterless constructor and settable properties; it instantiates these via reflection even
    // though they're private, which is fine (Activator.CreateInstance resolves non-public constructors
    // for types in the same assembly as the caller).
    private class FactsRow
    {
        public int Id { get; set; }
        public int Instrument { get; set; }
        public int Difficulty { get; set; }
        public int Score { get; set; }
        public int Stars { get; set; }
        public int NotesHit { get; set; }
        public int NotesMissed { get; set; }
        public int IsFc { get; set; }
        public double? Percent { get; set; }
    }

    private sealed class NewestRow : FactsRow
    {
        public string Hash { get; set; } = "";
        public long DateTicks { get; set; }
    }

    public PerformanceFacts? Resolve(ProvenanceLink link)
    {
        List<FactsRow> rows = _db.Query<FactsRow>(
            "SELECT Id, Instrument, Difficulty, Score, Stars, NotesHit, NotesMissed, IsFc, Percent " +
            "FROM PlayerScores WHERE Id = ? AND COALESCE(IsReplay,0)=0 LIMIT 1", link.PlayerScoreRecordId);
        return rows.Count == 0 ? null : ToFacts(rows[0]);
    }

    // MAX(Id) is a scalar aggregate, not a mapped row: sqlite-net's Query<T> matches result COLUMNS onto
    // a POCO's PROPERTIES by name, and `int` has none, so Query<int> would silently always yield [0]
    // rather than the real value. ExecuteScalar<T> is sqlite-net's correct API for a single-column,
    // single-row scalar read — this is a deliberate deviation from the task-9 brief's `Query<int>` draft.
    public int MaxGameRecordId() => _db.ExecuteScalar<int>("SELECT COALESCE(MAX(Id),0) FROM GameRecords");

    // The newest non-replay play of this song, for this profile/instrument/difficulty, recorded AFTER the
    // baseline GameRecord rowid — the quest-launched play. Null if none (quit / invalid / not launched).
    public PlayRecord? NewestSince(
        int baselineGameRecordId, string songHashHex, Guid profileId, Instrument instrument, Difficulty difficulty)
    {
        List<NewestRow> rows = _db.Query<NewestRow>(
            "SELECT ps.Id AS Id, ps.Instrument AS Instrument, ps.Difficulty AS Difficulty, ps.Score AS Score, " +
            "ps.Stars AS Stars, ps.NotesHit AS NotesHit, ps.NotesMissed AS NotesMissed, ps.IsFc AS IsFc, " +
            "ps.Percent AS Percent, lower(hex(gr.SongChecksum)) AS Hash, gr.Date AS DateTicks " +
            "FROM PlayerScores ps JOIN GameRecords gr ON ps.GameRecordId = gr.Id " +
            "WHERE gr.Id > ? AND lower(hex(gr.SongChecksum)) = ? AND ps.PlayerId = ? " +
            "AND ps.Instrument = ? AND ps.Difficulty = ? AND COALESCE(ps.IsReplay,0)=0 " +
            "ORDER BY gr.Id DESC LIMIT 1",
            baselineGameRecordId, songHashHex.ToLowerInvariant(), profileId.ToString(),
            (int)instrument, (int)difficulty);

        if (rows.Count == 0)
        {
            return null;
        }

        NewestRow r = rows[0];
        return new PlayRecord(r.Id, r.Hash, new DateTime(r.DateTicks), ToFacts(r));
    }

    private static PerformanceFacts ToFacts(FactsRow r)
    {
        double percent = r.Percent ?? (r.NotesHit + r.NotesMissed == 0
            ? 0.0
            : (double)r.NotesHit / (r.NotesHit + r.NotesMissed));
        return new PerformanceFacts(percent, r.IsFc != 0, r.Stars, r.NotesHit, r.NotesMissed, (Difficulty)r.Difficulty);
    }

    public void Dispose() => _db.Close();
}
