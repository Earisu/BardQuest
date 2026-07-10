namespace BardQuest.Domain.Ratings;

/// <summary>Instrument-agnostic note-density math over onset times (seconds, ascending). Peak uses a
/// robust percentile of a sliding-window rate so a single grace cluster can't spike it.</summary>
public static class NoteDensity
{
    public const double PeakWindowSeconds = 1.0;
    public const double PeakPercentile = 0.90;

    public static double AvgNps(int noteCount, double durationSeconds)
        => durationSeconds > 0 ? noteCount / durationSeconds : 0.0;

    /// <summary>90th-percentile notes/sec over a 1s sliding window. 0 for fewer than two notes.</summary>
    public static double PeakNps(IReadOnlyList<double> sortedTimes)
        => sortedTimes.Count < 2 ? 0.0 : Percentile(WindowedRates(sortedTimes, PeakWindowSeconds), PeakPercentile);

    /// <summary>Maximum windowed notes/sec for an arbitrary window — the raw burst rate.</summary>
    public static double PeakWindowNps(IReadOnlyList<double> sortedTimes, double windowSeconds)
    {
        return sortedTimes.Count < 2 || windowSeconds <= 0 ? 0.0 : WindowedRates(sortedTimes, windowSeconds).Max();
    }

    /// <summary>Nearest-rank percentile (does not mutate input).</summary>
    public static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sorted = values.OrderBy(x => x).ToList();
        int rank = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }

    // Per-onset rate: notes within [t, t+window) / window. Assumes ascending input.
    private static double[] WindowedRates(IReadOnlyList<double> sortedTimes, double window)
    {
        double[] rates = new double[sortedTimes.Count];
        for (int i = 0; i < sortedTimes.Count; i++)
        {
            int hi = i;
            while (hi + 1 < sortedTimes.Count && sortedTimes[hi + 1] - sortedTimes[i] < window)
            {
                hi++;
            }

            rates[i] = (hi - i + 1) / window;
        }

        return rates;
    }
}
