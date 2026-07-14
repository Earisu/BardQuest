using BardQuest.Domain.Progression;

namespace BardQuest.Domain.Quest;

/// <summary>Resolves a provenance link to the scores.db play it points at. Implemented in the Mod over a
/// read-only scores.db connection; faked in Domain tests.</summary>
public interface IScoreSource
{
    PerformanceFacts? Resolve(ProvenanceLink link);
}
