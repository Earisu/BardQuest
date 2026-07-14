extern alias yargpkg;

using YARG.Song; // SongContainer (Assembly-CSharp)

using RtSongEntry = yargpkg::YARG.Core.Song.SongEntry;

namespace BardQuest.Mod.Quest;

// Single source for hash -> runtime SongEntry lookups over YARG's SongContainer. The match mirrors
// scores.db provenance (case-insensitive hex of the chart checksum) and is shared by the launcher, the
// metadata enricher, and the preview player so their song resolution can never drift apart.
internal static class SongCatalog
{
    public static RtSongEntry? ByHash(string songHashHex)
    {
        foreach (RtSongEntry e in SongContainer.Songs)
        {
            if (string.Equals(e.Hash.ToString(), songHashHex, StringComparison.OrdinalIgnoreCase))
            {
                return e;
            }
        }

        return null;
    }
}
