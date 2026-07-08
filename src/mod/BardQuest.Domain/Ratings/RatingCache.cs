using System.Text;

using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>
/// Versioned binary store for chart ratings, keyed by song hash. Layout: [uint Magic][int Version]
/// [int songCount], then per song: [string hash][int ratingCount], then per rating:
/// [byte Instrument][byte Difficulty][int Tier][double SubScore][double RepresentativeNps].
/// One sequential pass, zero external dependencies (loads under Mono). A magic/version mismatch or a
/// truncated stream yields null, so the caller rebuilds from scratch (the migration lever).
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
            var result = new Dictionary<string, List<ChartRating>>(songCount);
            for (int i = 0; i < songCount; i++)
            {
                string hash = r.ReadString();
                int ratingCount = r.ReadInt32();
                var ratings = new List<ChartRating>(ratingCount);
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
        catch (EndOfStreamException)
        {
            return null; // truncated/corrupt — treat as no cache, rebuild
        }
    }
}
