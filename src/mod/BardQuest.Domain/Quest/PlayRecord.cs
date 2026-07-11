// src/mod/BardQuest.Domain/Quest/PlayRecord.cs
using BardQuest.Domain.Progression;

namespace BardQuest.Domain.Quest;

/// <summary>A scores.db play row as the Mod's correlation query returns it — the rowid, song hash, date
/// and facts — used to mint a <see cref="ProvenanceLink"/> after a quest-launched play.</summary>
public sealed record PlayRecord(int PlayerScoreRecordId, string SongHashHex, DateTime PlayedAt, PerformanceFacts Facts);
