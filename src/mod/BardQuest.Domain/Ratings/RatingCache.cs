using System.Text;

using BardQuest.Domain.Ratings.Drums;

using YARG.Core;

namespace BardQuest.Domain.Ratings;

/// <summary>Versioned binary store for chart metrics, keyed by song hash. Stores RAW measurements
/// only (attributes/rank are derived on load). Per chart: identity + a family raw payload written by
/// an instrument-dispatched codec (drums only today). Any magic/version mismatch, unknown instrument,
/// truncated stream, or bogus count yields null so the caller rebuilds (the migration lever).</summary>
public static class RatingCache
{
    public const uint Magic = 0x42515243; // "BQRC"
    public const int Version = 2; // v2: independence-rate + fastest-kick-span raw fields

    public static void Serialize(IReadOnlyDictionary<string, IReadOnlyList<ChartMetrics>> byHash, Stream stream)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(Version);
        w.Write(byHash.Count);
        foreach (KeyValuePair<string, IReadOnlyList<ChartMetrics>> song in byHash)
        {
            w.Write(song.Key);
            w.Write(song.Value.Count);
            foreach (ChartMetrics c in song.Value)
            {
                w.Write((byte)c.Instrument);
                w.Write((byte)c.Difficulty);
                w.Write(c.Intensity);
                WriteRaw(w, c.Instrument, c.Raw);
            }
        }
    }

    public static Dictionary<string, List<ChartMetrics>>? Deserialize(Stream stream)
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

            var result = new Dictionary<string, List<ChartMetrics>>();
            for (int i = 0; i < songCount; i++)
            {
                string hash = r.ReadString();
                int chartCount = r.ReadInt32();
                if (chartCount < 0)
                {
                    return null;
                }

                var charts = new List<ChartMetrics>(chartCount);
                for (int j = 0; j < chartCount; j++)
                {
                    var instrument = (Instrument)r.ReadByte();
                    var difficulty = (Difficulty)r.ReadByte();
                    int intensity = r.ReadInt32();
                    IRawMetrics? raw = ReadRaw(r, instrument);
                    if (raw == null)
                    {
                        return null; // unknown instrument codec
                    }

                    charts.Add(new ChartMetrics(instrument, difficulty, intensity, raw));
                }

                result[hash] = charts;
            }

            return result;
        }
        catch (Exception)
        {
            return null; // truncated / corrupt
        }
    }

    // --- instrument-dispatched raw codec (drums only today) ---

    private static void WriteRaw(BinaryWriter w, Instrument instrument, IRawMetrics raw)
    {
        switch (instrument)
        {
            case Instrument.ProDrums:
                WriteDrum(w, (DrumRawMetrics)raw);
                break;
            default:
                throw new NotSupportedException($"No raw codec for {instrument}");
        }
    }

    private static IRawMetrics? ReadRaw(BinaryReader r, Instrument instrument) => instrument switch
    {
        Instrument.ProDrums => ReadDrum(r),
        _ => null,
    };

    private static void WriteDrum(BinaryWriter w, DrumRawMetrics m)
    {
        w.Write(m.AvgNps);
        w.Write(m.PeakNps);
        w.Write(m.LongestDenseSectionSeconds);
        w.Write(m.KickDensity);
        w.Write(m.LongestKickRun);
        w.Write(m.PeakBurstNps);
        w.Write(m.FastFillRate);
        w.Write(m.ShortestTransitionGap);
        w.Write(m.PatternVariety);
        w.Write(m.OffCarrierPerSec);
        w.Write(m.OffCarrierFastPerSec);
        w.Write(m.ResidualAltPerSec);
        w.Write(m.NoCarrierAltPerSec);
        w.Write(m.FastestKickSpanNps);
        w.Write(m.KitPieceEntropy);
    }

    private static DrumRawMetrics ReadDrum(BinaryReader r)
        => new(
            r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadInt32(), r.ReadDouble(),
            r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble(),
            r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
}
