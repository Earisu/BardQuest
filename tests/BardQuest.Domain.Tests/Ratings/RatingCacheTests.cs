using BardQuest.Domain.Ratings;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Ratings;

public class RatingCacheTests
{
    private static Dictionary<string, IReadOnlyList<ChartRating>> Sample() => new()
    {
        ["AABBCC"] =
        [
            new ChartRating(Instrument.ProDrums, Difficulty.Expert, 5, 0.42, 12.5),
            new ChartRating(Instrument.ProDrums, Difficulty.Hard, 4, 0.18, 7.0),
        ],
        ["DDEEFF"] =
        [
            new ChartRating(Instrument.ProDrums, Difficulty.Expert, 3, 0.0, 4.0),
        ],
    };

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips()
    {
        Dictionary<string, IReadOnlyList<ChartRating>> input = Sample();
        using var ms = new MemoryStream();
        RatingCache.Serialize(input.ToDictionary(k => k.Key, v => v.Value), ms);
        ms.Position = 0;

        Dictionary<string, List<ChartRating>>? outp = RatingCache.Deserialize(ms);

        Assert.NotNull(outp);
        Assert.Equal(2, outp.Count);
        Assert.Equal(input["AABBCC"], outp["AABBCC"]);       // records compare by value
        Assert.Equal(input["DDEEFF"], outp["DDEEFF"]);
    }

    [Fact]
    public void Deserialize_WrongMagic_ReturnsNull()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(0xDEADBEEF); // not RatingCache.Magic
        }

        ms.Position = 0;
        Assert.Null(RatingCache.Deserialize(ms));
    }

    [Fact]
    public void Deserialize_WrongVersion_ReturnsNull()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(RatingCache.Magic);
            w.Write(RatingCache.Version + 1); // future version
        }

        ms.Position = 0;
        Assert.Null(RatingCache.Deserialize(ms));
    }

    [Fact]
    public void Deserialize_EmptyStream_ReturnsNull()
    {
        using var ms = new MemoryStream();
        Assert.Null(RatingCache.Deserialize(ms));
    }
}
