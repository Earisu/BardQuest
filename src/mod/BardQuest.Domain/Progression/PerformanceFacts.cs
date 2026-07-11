using YARG.Core;

namespace BardQuest.Domain.Progression;

/// <summary>The scores.db projection of one completed play — the inputs the XP formula reads. Only
/// <see cref="Percent"/> and <see cref="IsFc"/> drive the first-pass formula; the rest are carried
/// faithfully from the score record for calibration and later consumers. Max-combo, overhits and
/// No-Fail state are deliberately absent: they are transient in YARG and never persisted, so BardQuest
/// cannot read them without a stats seam it does not add.</summary>
public sealed record PerformanceFacts(
    double Percent,
    bool IsFc,
    int Stars,
    int NotesHit,
    int NotesMissed,
    Difficulty Difficulty);
