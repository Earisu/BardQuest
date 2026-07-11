namespace BardQuest.Domain.Ratings;

/// <summary>A chart's five attribute scores (each in [0,10]). Descriptive — never summed into a
/// difficulty by consumers; the overall <see cref="Rank"/> is derived from it by
/// <see cref="RankDerivation"/>. A missing attribute reads 0.</summary>
public sealed class AttributeProfile(IReadOnlyDictionary<Attribute, double> scores)
{
    public IReadOnlyDictionary<Attribute, double> Scores { get; } = scores;

    public double this[Attribute a] => Scores.TryGetValue(a, out double v) ? v : 0.0;

    /// <summary>Unweighted sum of all five axes (0..50).</summary>
    public double Sum()
    {
        double total = 0;
        foreach (Attribute a in Enum.GetValues(typeof(Attribute)))
        {
            total += this[a];
        }

        return total;
    }

    /// <summary>Highest single attribute (0..10) — the chart's "threat level" for the monster sheet:
    /// a lopsided specialist and a well-rounded chart with the same <see cref="Sum"/> read differently.</summary>
    public double Threat()
    {
        double max = 0;
        foreach (Attribute a in Enum.GetValues(typeof(Attribute)))
        {
            max = Math.Max(max, this[a]);
        }

        return max;
    }
}
