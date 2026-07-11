namespace BardQuest.Domain.Ratings;

/// <summary>A family's typed raw-measurement record, carried by <see cref="ChartMetrics"/> and
/// serialized by the instrument-dispatched cache codec. Each family knows how to derive its own five
/// attribute scores from its raw measurements (run on load, never stored) — see
/// <see cref="ToAttributeProfile"/>.</summary>
public interface IRawMetrics
{
    /// <summary>Derives the five 0–10 attribute scores this chart demands. Family-specific: each raw
    /// record maps its own measurements onto the shared attribute axes.</summary>
    AttributeProfile ToAttributeProfile();
}
