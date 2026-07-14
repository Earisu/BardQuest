extern alias yargpkg;

using UnityEngine;

using YARG.Helpers.Extensions; // LoadTexture(this YARGImage, bool)

using yargpkg::YARG.Core.IO;   // YARGImage

using RtSongEntry = yargpkg::YARG.Core.Song.SongEntry;

namespace BardQuest.Mod.Quest;

// Turns a monster's song hash into displayable metadata (title, artist, album cover). Album textures are
// loaded on demand and cached; the working set is small (<=5) so synchronous loads are acceptable.
public sealed class SongEnricher
{
    public readonly record struct SongInfo(string Title, string Artist, Texture2D? Album);

    // Nullable value: a stored null is a cached *miss* (hash not in the library), distinct from an absent
    // key (never looked up). Caching misses stops a stale rating-cache hash from re-walking every song in
    // SongContainer on each highlight.
    private readonly Dictionary<string, SongInfo?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SongInfo? Lookup(string songHashHex)
    {
        if (_cache.TryGetValue(songHashHex, out SongInfo? hit))
        {
            return hit;
        }

        RtSongEntry? entry = SongCatalog.ByHash(songHashHex);
        if (entry == null)
        {
            _cache[songHashHex] = null;
            return null;
        }

        Texture2D? album = null;
        try
        {
            using YARGImage image = entry.LoadAlbumData();
            if (image != null)
            {
                album = image.LoadTexture(false);
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn("Album load failed for " + songHashHex + ": " + ex.Message);
        }

        var info = new SongInfo(entry.Name.ToString(), entry.Artist.ToString(), album);
        _cache[songHashHex] = info;
        return info;
    }

    // Destroys every cached album texture and clears the cache. Album textures are Unity native objects the
    // GC never frees, so they must be Destroyed explicitly or they accumulate for the whole session. Called
    // when the Hub goes away for a fight; the small working set is cheaply re-enriched on return.
    public void Teardown()
    {
        foreach (SongInfo? entry in _cache.Values)
        {
            if (entry is { Album: { } album })
            {
                UnityEngine.Object.Destroy(album);
            }
        }

        _cache.Clear();
    }
}
