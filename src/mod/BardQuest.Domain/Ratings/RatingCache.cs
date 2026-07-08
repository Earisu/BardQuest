using System.Text;

using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>
/// Versioned binary store for chart ratings, keyed by song hash. Layout: [uint Magic][int Version]
/// [int songCount], then per song: [string hash][int ratingCount], then per rating:
/// [byte Instrument][byte Difficulty][int Tier][double SubScore][double RepresentativeNps].
/// One sequential pass, zero external dependencies (loads under Mono). A magic/version mismatch, a
/// truncated stream, or a corrupt (e.g. bogus/negative) count yields null, so the caller rebuilds from
/// scratch (the migration lever).
/// </summary>
public static class RatingCache
{
    public const uint Magic = 0x42515243; // "BQRC"
    public const int Version = 1;

    public static void Serialize(IReadOnlyDictionary<string, IReadOnlyList<ChartRating>> byHash, Stream stream)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(Version);
        w.Write(byHash.Count);
        foreach (KeyValuePair<string, IReadOnlyList<ChartRating>> song in byHash)
        {
            w.Write(song.Key);
            w.Write(song.Value.Count);
            foreach (ChartRating r in song.Value)
            {
                w.Write((byte)r.Instrument);
                w.Write((byte)r.Difficulty);
                w.Write(r.Tier);
                w.Write(r.SubScore);
                w.Write(r.RepresentativeNps);
            }
        }
    }

    public static Dictionary<string, List<ChartRating>>? Deserialize(Stream stream)
    {
        try
        {
            using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (r.ReadUInt32() != Magic || r.ReadInt32() != Version)
            {
                return null;
            }

            int songCount = r.ReadInt32();
            if (songCount < 0)
            {
                return null;
            }

            var result = new Dictionary<string, List<ChartRating>>();
            for (int i = 0; i < songCount; i++)
            {
                string hash = r.ReadString();
                int ratingCount = r.ReadInt32();
                if (ratingCount < 0)
                {
                    return null;
                }

                var ratings = new List<ChartRating>();
                for (int j = 0; j < ratingCount; j++)
                {
                    var instrument = (Instrument)r.ReadByte();
                    var difficulty = (Difficulty)r.ReadByte();
                    int tier = r.ReadInt32();
                    double subScore = r.ReadDouble();
                    double repNps = r.ReadDouble();
                    ratings.Add(new ChartRating(instrument, difficulty, tier, subScore, repNps));
                }

                result[hash] = ratings;
            }

            return result;
        }
        catch (Exception)
        {
            return null; // truncated/corrupt (short stream or a bogus count) — treat as no cache, rebuild
        }
    }
}
