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

    private readonly Dictionary<string, SongInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SongInfo? Lookup(string songHashHex)
    {
        if (_cache.TryGetValue(songHashHex, out SongInfo hit))
        {
            return hit;
        }

        RtSongEntry? entry = SongCatalog.ByHash(songHashHex);
        if (entry == null)
        {
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
}
