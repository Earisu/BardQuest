namespace BardQuest.Domain.Ratings;

public static class ChartAnalyzer
{
    private static readonly DrumPad[] TomPads =
        [DrumPad.YellowDrum, DrumPad.BlueDrum, DrumPad.GreenDrum];

    private static readonly DrumPad[] CymbalPads =
        [DrumPad.YellowCymbal, DrumPad.BlueCymbal, DrumPad.GreenCymbal];

    public static IReadOnlyList<string> Analyze(
        IEnumerable<(double Time, DrumPad Pad)> hits, double densityThreshold = 8.0)
    {
        (double Time, DrumPad Pad)[] arr = [.. hits.OrderBy(h => h.Time)];
        var tags = new List<string>();

        if (HasFastBass(arr))
        {
            tags.Add("fast-bass");
        }

        if (HasIndependence(arr))
        {
            tags.Add("independence");
        }

        if (HasTomHeavy(arr))
        {
            tags.Add("tom-heavy");
        }

        if (HasCymbalHeavy(arr))
        {
            tags.Add("cymbal-heavy");
        }

        if (HasHighDensity(arr, densityThreshold))
        {
            tags.Add("dense");
        }

        return tags;
    }

    public static bool HasFastBass(IEnumerable<(double Time, DrumPad Pad)> hits)
    {
        var kicks = hits.Where(h => h.Pad == DrumPad.Kick)
            .OrderBy(h => h.Time)
            .ToList();
        for (int i = 0; i <= kicks.Count - 3; i++)
        {
            if (kicks[i + 2].Time - kicks[i].Time <= 0.3)
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasIndependence(IEnumerable<(double Time, DrumPad Pad)> hits)
    {
        (double Time, DrumPad Pad)[] arr = [.. hits];
        var grid = arr.Where(h => h.Pad is DrumPad.RedDrum or DrumPad.YellowCymbal)
            .OrderBy(h => h.Time)
            .ToList();
        if (grid.Count < 2)
        {
            return false;
        }

        double avgBeat = Enumerable.Range(0, grid.Count - 1)
            .Select(i => grid[i + 1].Time - grid[i].Time)
            .Where(d => d > 0)
            .DefaultIfEmpty(0.5)
            .Average();
        double eighthNote = avgBeat / 2.0;

        var kicks = arr.Where(h => h.Pad == DrumPad.Kick).ToList();
        if (kicks.Count == 0)
        {
            return false;
        }

        int offBeat = kicks.Count(kick =>
        {
            double nearest = grid.MinBy(g => Math.Abs(g.Time - kick.Time)).Time;
            return Math.Abs(kick.Time - nearest) >= eighthNote;
        });

        return (double)offBeat / kicks.Count > 0.2;
    }

    public static bool HasTomHeavy(IEnumerable<(double Time, DrumPad Pad)> hits)
    {
        var nonKick = hits.Where(h => h.Pad != DrumPad.Kick).ToList();
        return nonKick.Count == 0 ? false : (double)nonKick.Count(h => TomPads.Contains(h.Pad)) / nonKick.Count > 0.6;
    }

    public static bool HasCymbalHeavy(IEnumerable<(double Time, DrumPad Pad)> hits)
    {
        var nonKick = hits.Where(h => h.Pad != DrumPad.Kick).ToList();
        return nonKick.Count == 0 ? false : (double)nonKick.Count(h => CymbalPads.Contains(h.Pad)) / nonKick.Count > 0.6;
    }

    public static bool HasHighDensity(IEnumerable<(double Time, DrumPad Pad)> hits, double threshold)
    {
        (double Time, DrumPad Pad)[] arr = [.. hits];
        if (arr.Length < 2)
        {
            return false;
        }

        double duration = arr.Max(h => h.Time) - arr.Min(h => h.Time);
        return duration <= 0 ? false : arr.Length / duration > threshold;
    }

    // --- Difficulty profile (Phase 1: nuanced per-chart difficulty) ---

    public const double PeakWindowSeconds = 1.0; // sliding window for "sustained burst" density
    public const double PeakPercentile = 0.90; // robust peak = this percentile of windowed rates
    public const double DoubleBassSpeedNorm = 10.0; // kicks/sec that maps double-bass speed to 1.0
    public const double EnduranceKickRate = 6.0; // kicks/sec a 1s window must reach to count as "sustained"
    public const double BlastRateNorm = 12.0; // snare+kick hits/sec that maps blast rate to 1.0

    public const double BlastAltThreshold = 0.85; // near-strict snare<->kick alternation needed for a full blast

    // (gallops like K,K,S only partially register — they're also fast,
    //  but not true blasts)
    public const double IndependenceCeil = 0.50; // off-grid kick fraction that maps independence to 1.0
    public const double FastFillNorm = 12.0; // snare+tom hits/sec that maps a fill to 1.0

    /// <summary>Hardest sustained density: the 90th-percentile notes/sec over a 1s sliding window.
    /// Percentile-not-max so a single flam/grace cluster can't spike it. Returns 0 for fewer than two notes; very short passages (under ~10 notes at the target rate) may under-report due to end-of-window truncation.</summary>
    public static double PeakNps(IReadOnlyList<(double Time, DrumPad Pad)> hits)
        => PeakRate(hits.Select(h => h.Time));

    /// <summary>Like <see cref="PeakNps"/> but assumes the input is already sorted ascending by Time.</summary>
    private static double PeakNpsSorted(IReadOnlyList<(double Time, DrumPad Pad)> sortedHits)
        => PeakRateSorted([.. sortedHits.Select(h => h.Time)]);

    /// <summary>Sustained density over the whole chart: total notes / duration.</summary>
    public static double AvgNps(IReadOnlyList<(double Time, DrumPad Pad)> hits, double durationSeconds)
        => durationSeconds > 0 ? hits.Count / durationSeconds : 0.0;

    /// <summary>The full difficulty profile for one charted difficulty's hits.</summary>
    public static ChartDifficultyProfile Profile(
        IReadOnlyList<(double Time, DrumPad Pad)> hits, double durationSeconds)
    {
        var sorted = hits.OrderBy(h => h.Time).ToList();
        return ProfileSorted(sorted, durationSeconds);
    }

    /// <summary>Same as <see cref="Profile"/> but assumes <paramref name="hits"/> is already sorted
    /// ascending by Time — no re-sort. Called from the scan loop which pre-sorts the hit list.</summary>
    public static ChartDifficultyProfile ProfileSorted(
        IReadOnlyList<(double Time, DrumPad Pad)> hits, double durationSeconds)
    {
        return new ChartDifficultyProfile(
            PeakNpsSorted(hits),
            AvgNps(hits, durationSeconds),
            DoubleBassS(hits),
            BlastBeatS(hits),
            IndependenceS(hits),
            FastFillS(hits));
    }

    // --- shared math ---

    /// <summary>The PeakPercentile of the per-onset windowed rate (notes within [t, t+window)) / window.</summary>
    private static double PeakRate(IEnumerable<double> times)
    {
        var t = times.OrderBy(x => x).ToList();
        return t.Count < 2 ? 0.0 : Percentile(WindowedRates(t), PeakPercentile);
    }

    /// <summary>Like <see cref="PeakRate"/> but assumes the input list is already sorted ascending.</summary>
    private static double PeakRateSorted(List<double> sortedTimes) =>
        sortedTimes.Count < 2 ? 0.0 : Percentile(WindowedRates(sortedTimes), PeakPercentile);

    // --- Sorted-aware internal helpers (assume ascending Time, no re-sort) ---

    private static double DoubleBassS(IReadOnlyList<(double Time, DrumPad Pad)> sorted)
    {
        var kicks = sorted.Where(h => h.Pad == DrumPad.Kick).Select(h => h.Time).ToList();
        if (kicks.Count < 2)
        {
            return 0.0;
        }

        // kicks are in ascending order because sorted is ascending and we preserved it via Where
        IReadOnlyList<double> rates = WindowedRates(kicks);
        double speed = Math.Clamp(Percentile(rates, PeakPercentile) / DoubleBassSpeedNorm, 0, 1);
        double endurance = rates.Count(r => r >= EnduranceKickRate) / (double)kicks.Count;
        return Math.Clamp((0.6 * speed) + (0.4 * Math.Clamp(endurance, 0, 1)), 0, 1);
    }

    private static double BlastBeatS(IReadOnlyList<(double Time, DrumPad Pad)> sorted)
    {
        var sk = sorted.Where(h => h.Pad is DrumPad.Kick or DrumPad.RedDrum).ToList();
        if (sk.Count < 4)
        {
            return 0.0;
        }

        double rateScore =
            Math.Clamp(Percentile(WindowedRates([.. sk.Select(h => h.Time)]), PeakPercentile) / BlastRateNorm, 0,
                1);
        int alternations = 0;
        for (int i = 1; i < sk.Count; i++)
        {
            if (sk[i].Pad != sk[i - 1].Pad)
            {
                alternations++;
            }
        }

        double altFraction = alternations / (double)(sk.Count - 1);
        double altScore = Math.Clamp(altFraction / BlastAltThreshold, 0, 1);
        return rateScore * altScore;
    }

    private static double IndependenceS(IReadOnlyList<(double Time, DrumPad Pad)> sorted)
    {
        // grid is ascending because sorted is ascending and Where preserves order
        double[] grid = [.. sorted.Where(h => h.Pad is DrumPad.RedDrum or DrumPad.YellowCymbal).Select(h => h.Time)];
        if (grid.Length < 2)
        {
            return 0.0;
        }

        double avgBeat = Enumerable.Range(0, grid.Length - 1).Select(i => grid[i + 1] - grid[i])
            .Where(d => d > 0).DefaultIfEmpty(0.5).Average();
        double eighth = avgBeat / 2.0;
        var kicks = sorted.Where(h => h.Pad == DrumPad.Kick).Select(h => h.Time).ToList();
        if (kicks.Count == 0)
        {
            return 0.0;
        }

        int offGrid = kicks.Count(k => NearestDistanceBinarySearch(grid, k) >= eighth);
        double offFraction = offGrid / (double)kicks.Count;
        return Math.Clamp(offFraction / IndependenceCeil, 0, 1);
    }

    private static double FastFillS(IReadOnlyList<(double Time, DrumPad Pad)> sorted)
    {
        var fill = sorted.Where(h => FillPads.Contains(h.Pad)).Select(h => h.Time).ToList();
        return fill.Count < 2 ? 0.0 : Math.Clamp(PeakRateSorted(fill) / FastFillNorm, 0, 1);
    }

    /// <summary>Returns the distance from <paramref name="k"/> to its nearest value in the sorted
    /// <paramref name="grid"/> array, using binary search — O(log n) instead of O(n).</summary>
    private static double NearestDistanceBinarySearch(double[] grid, double k)
    {
        int idx = Array.BinarySearch(grid, k);
        if (idx >= 0)
        {
            return 0.0; // exact match
        }

        int ins = ~idx; // insertion point
        double best = double.MaxValue;
        if (ins < grid.Length)
        {
            best = Math.Min(best, Math.Abs(grid[ins] - k));
        }

        if (ins > 0)
        {
            best = Math.Min(best, Math.Abs(grid[ins - 1] - k));
        }

        return best;
    }

    /// <summary>Nearest-rank percentile of the values (does not mutate the input).</summary>
    private static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sorted = values.OrderBy(x => x).ToList();
        int rank = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }

    private static readonly DrumPad[] FillPads =
        [DrumPad.RedDrum, DrumPad.YellowDrum, DrumPad.BlueDrum, DrumPad.GreenDrum];

    /// <summary>Double-bass difficulty: 60% peak kick speed + 40% endurance. Endurance is the fraction
    /// of kick onsets sitting inside a sustained fast-kick passage — so a short double-bass burst in an
    /// otherwise slow chart scores lower endurance than a relentlessly fast chart (by design: peak speed
    /// already credits the hard passage; endurance rewards sustaining it).</summary>
    public static double DoubleBass(IReadOnlyList<(double Time, DrumPad Pad)> hits)
    {
        var kicks = hits.Where(h => h.Pad == DrumPad.Kick).Select(h => h.Time).OrderBy(t => t).ToList();
        if (kicks.Count < 2)
        {
            return 0.0;
        }

        IReadOnlyList<double> rates = WindowedRates(kicks);
        double speed = Math.Clamp(Percentile(rates, PeakPercentile) / DoubleBassSpeedNorm, 0, 1);
        double endurance = rates.Count(r => r >= EnduranceKickRate) / (double)kicks.Count;
        return Math.Clamp((0.6 * speed) + (0.4 * Math.Clamp(endurance, 0, 1)), 0, 1);
    }

    /// <summary>Blast beats: fast snare+kick rate (rateScore) gated by how strictly they alternate (altScore).
    /// Gallop patterns (e.g. K,K,S) alternate less, so they only partially register.</summary>
    public static double BlastBeat(IReadOnlyList<(double Time, DrumPad Pad)> hits)
    {
        var sk = hits.Where(h => h.Pad is DrumPad.Kick or DrumPad.RedDrum)
            .OrderBy(h => h.Time).ToList();
        if (sk.Count < 4)
        {
            return 0.0;
        }

        double rateScore =
            Math.Clamp(Percentile(WindowedRates([.. sk.Select(h => h.Time)]), PeakPercentile) / BlastRateNorm, 0,
                1);
        int alternations = 0;
        for (int i = 1; i < sk.Count; i++)
        {
            if (sk[i].Pad != sk[i - 1].Pad)
            {
                alternations++;
            }
        }

        double altFraction = alternations / (double)(sk.Count - 1);
        double altScore = Math.Clamp(altFraction / BlastAltThreshold, 0, 1);
        return rateScore * altScore;
    }

    /// <summary>Limb independence: graded fraction of kicks landing off the hi-hat/snare grid.</summary>
    public static double Independence(IReadOnlyList<(double Time, DrumPad Pad)> hits)
    {
        double[] grid = [.. hits.Where(h => h.Pad is DrumPad.RedDrum or DrumPad.YellowCymbal)
            .OrderBy(h => h.Time).Select(h => h.Time)];
        if (grid.Length < 2)
        {
            return 0.0;
        }

        double avgBeat = Enumerable.Range(0, grid.Length - 1).Select(i => grid[i + 1] - grid[i])
            .Where(d => d > 0).DefaultIfEmpty(0.5).Average();
        double eighth = avgBeat / 2.0;
        var kicks = hits.Where(h => h.Pad == DrumPad.Kick).Select(h => h.Time).ToList();
        if (kicks.Count == 0)
        {
            return 0.0;
        }

        int offGrid = kicks.Count(k => NearestDistanceBinarySearch(grid, k) >= eighth);
        double offFraction = offGrid / (double)kicks.Count;
        return Math.Clamp(offFraction / IndependenceCeil, 0, 1);
    }

    /// <summary>Fast fills: peak burst rate of snare+tom (non-kick, non-cymbal) notes.</summary>
    public static double FastFill(IReadOnlyList<(double Time, DrumPad Pad)> hits)
    {
        var fill = hits.Where(h => FillPads.Contains(h.Pad)).Select(h => h.Time).OrderBy(t => t).ToList();
        return fill.Count < 2 ? 0.0 : Math.Clamp(PeakRate(fill) / FastFillNorm, 0, 1);
    }

    /// <summary>Per-onset windowed rate (notes within [t, t+PeakWindowSeconds)) / window — the raw values PeakRate ranks.</summary>
    private static double[] WindowedRates(List<double> sortedTimes)
    {
        double[] rates = new double[sortedTimes.Count];
        for (int i = 0; i < sortedTimes.Count; i++)
        {
            int hi = i;
            while (hi + 1 < sortedTimes.Count && sortedTimes[hi + 1] - sortedTimes[i] < PeakWindowSeconds)
            {
                hi++;
            }

            rates[i] = (hi - i + 1) / PeakWindowSeconds;
        }

        return rates;
    }
}
