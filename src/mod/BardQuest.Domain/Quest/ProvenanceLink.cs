namespace BardQuest.Domain.Quest;

/// <summary>A link to one real YARG play launched through the quest: the scores.db PlayerScores rowid
/// (→ PerformanceFacts), the song hash (→ the chart's AttributeProfile), and the play date (orders the
/// chronological fold). No score values are copied — only the record identifier.</summary>
public sealed record ProvenanceLink(int PlayerScoreRecordId, string SongHashHex, DateTime PlayedAt);
