using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>A queryable, in-memory view over the deserialized rating cache ({hash → charts}), fixed to
/// one (instrument, difficulty) — the pair a quest is scoped to. Each song contributes at most one
/// monster (its chart at that instrument/difficulty), with the AttributeProfile derived on load from the
/// raw metrics. Sentinel charts (Intensity &lt; 0 — attempted but unrateable) are excluded.</summary>
public sealed class RatedLibrary
{
    private readonly Dictionary<string, AttributeProfile> _profiles = [];

    public RatedLibrary(
        IReadOnlyDictionary<string, IReadOnlyList<ChartMetrics>> byHash, Instrument instrument, Difficulty difficulty)
    {
        foreach (KeyValuePair<string, IReadOnlyList<ChartMetrics>> song in byHash)
        {
            foreach (ChartMetrics c in song.Value)
            {
                if (c.Instrument == instrument && c.Difficulty == difficulty && c.Intensity >= 0)
                {
                    _profiles[song.Key] = c.Raw.ToAttributeProfile();
                    break;
                }
            }
        }
    }

    /// <summary>The chart's five-axis profile for a song hash, or null if it has no rateable chart here.</summary>
    public AttributeProfile? Profile(string hash) => _profiles.TryGetValue(hash, out AttributeProfile? p) ? p : null;

    /// <summary>All monsters (hash, profile, RankScore) ordered easiest-first by <see cref="AttributeProfile.Sum"/>.</summary>
    public IReadOnlyList<(string Hash, AttributeProfile Profile, double Sum)> Songs()
        => _profiles
            .Select(kv => (kv.Key, kv.Value, kv.Value.Sum()))
            .OrderBy(s => s.Item3)
            .ToList();
}
