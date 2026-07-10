namespace BardQuest.Domain.Ratings.Drums;

/// <summary>Measures a single ProDrums (or future drum-variant) chart's <see cref="DrumRawMetrics"/>
/// from role-tagged notes + a <see cref="SyncInfo"/>. Pure; runs on the scan's background threads.
/// One algorithm for all drum variants — the variant differences live in <see cref="DrumKitMap"/>.
/// Constants here are first-pass calibration targets.</summary>
public static partial class DrumChartAnalysis
{
    // --- Strength ---
    public const double DenseThresholdNps = 8.0; // 1s-window rate a passage must hold to count as "dense"

    internal static double AvgNps(IReadOnlyList<RoleNote> notes, double dur)
        => NoteDensity.AvgNps(notes.Count, dur);

    internal static double PeakNps(IReadOnlyList<RoleNote> notes)
        => NoteDensity.PeakNps(Times(notes));

    /// <summary>Longest contiguous span whose per-onset 1s-window rate stays at/above the threshold.</summary>
    internal static double LongestDenseSectionSeconds(IReadOnlyList<RoleNote> notes, double threshold)
    {
        if (notes.Count < 2)
        {
            return 0.0;
        }

        // Per-onset 1s-window rate (same definition NoteDensity uses).
        int n = notes.Count;
        double best = 0, runStart = double.NaN;
        for (int i = 0; i < n; i++)
        {
            int hi = i;
            while (hi + 1 < n && notes[hi + 1].Time - notes[i].Time < NoteDensity.PeakWindowSeconds)
            {
                hi++;
            }

            double rate = (hi - i + 1) / NoteDensity.PeakWindowSeconds;
            if (rate >= threshold)
            {
                if (double.IsNaN(runStart))
                {
                    runStart = notes[i].Time;
                }

                best = Math.Max(best, notes[i].Time - runStart);
            }
            else
            {
                runStart = double.NaN;
            }
        }

        return best;
    }

    internal static List<double> Times(IReadOnlyList<RoleNote> notes)
    {
        var t = new List<double>(notes.Count);
        foreach (RoleNote n in notes)
        {
            t.Add(n.Time);
        }

        return t;
    }

    // --- Endurance ---
    public const double KickRunMaxGap = 0.30;  // seconds between kicks to still count as one continuous run

    internal static double KickDensity(IReadOnlyList<RoleNote> notes, double dur)
        => NoteDensity.AvgNps(CountRole(notes, DrumRole.Kick), dur);

    internal static int LongestKickRun(IReadOnlyList<RoleNote> notes, double maxGapSeconds)
    {
        List<double> kicks = KickTimes(notes);
        if (kicks.Count == 0)
        {
            return 0;
        }

        int best = 1, cur = 1;
        for (int i = 1; i < kicks.Count; i++)
        {
            cur = kicks[i] - kicks[i - 1] <= maxGapSeconds ? cur + 1 : 1;
            best = Math.Max(best, cur);
        }

        return best;
    }

    // Fastest sustained kick span, window-free: max over i of (n-1)/(t[i+n-1]-t[i]). A windowed peak
    // rate would quantise to steps of 1/window (the library collapsed to {2,4,6,8}); this is continuous,
    // so Endurance can tell 6.2 kicks/s feet from 7.7.
    public const int KickSpanNotes = 8; // long enough to mean "sustained", short enough to catch bursts

    internal static double FastestKickSpanNps(IReadOnlyList<RoleNote> notes)
    {
        List<double> kicks = KickTimes(notes);
        double best = 0;
        for (int i = 0; i + KickSpanNotes - 1 < kicks.Count; i++)
        {
            double dt = kicks[i + KickSpanNotes - 1] - kicks[i];
            if (dt > 0)
            {
                best = Math.Max(best, (KickSpanNotes - 1) / dt);
            }
        }

        return best;
    }

    private static int CountRole(IReadOnlyList<RoleNote> notes, DrumRole role)
    {
        int c = 0;
        foreach (RoleNote n in notes)
        {
            if (n.Role == role)
            {
                c++;
            }
        }

        return c;
    }

    private static List<double> KickTimes(IReadOnlyList<RoleNote> notes)
    {
        var t = new List<double>();
        foreach (RoleNote n in notes)
        {
            if (n.Role == DrumRole.Kick)
            {
                t.Add(n.Time);
            }
        }

        return t; // already ascending (notes are ascending)
    }

    // --- Technique --- (limb-independence rates: see DrumChartAnalysis.Independence.cs)

    // --- Agility ---
    public const double BurstWindow = 0.5;          // window for peak burst / fill rate
    public const double TransitionGapPercentile = 0.05; // robust "fastest sustained" spacing

    internal static double PeakBurstNps(IReadOnlyList<RoleNote> notes)
        => NoteDensity.PeakWindowNps(Times(notes), BurstWindow);

    internal static double FastFillRate(IReadOnlyList<RoleNote> notes)
    {
        var fill = notes.Where(n => n.Role is DrumRole.Snare or DrumRole.Tom).Select(n => n.Time).ToList();
        return NoteDensity.PeakWindowNps(fill, BurstWindow);
    }

    internal static double ShortestTransitionGap(IReadOnlyList<RoleNote> notes)
    {
        if (notes.Count < 3)
        {
            return 0.0;
        }

        var gaps = new List<double>(notes.Count - 1);
        for (int i = 1; i < notes.Count; i++)
        {
            double g = notes[i].Time - notes[i - 1].Time;
            if (g > 0)
            {
                gaps.Add(g);
            }
        }

        return gaps.Count == 0 ? 0.0 : NoteDensity.Percentile(gaps, TransitionGapPercentile);
    }

    // --- Precision ---
    public const double SyncTolerance = 0.05; // phase distance from a strong beat to still count "on"

    private static double BeatPhase(uint tick, uint resolution) => tick % resolution / (double)resolution;

    internal static double SyncopationFraction(IReadOnlyList<RoleNote> notes, uint resolution)
    {
        if (resolution == 0 || notes.Count == 0)
        {
            return 0.0;
        }

        int off = 0;
        foreach (RoleNote n in notes)
        {
            double phase = BeatPhase(n.Tick, resolution);
            double dStrong = Math.Min(PhaseDist(phase, 0.0), PhaseDist(phase, 0.5));
            if (dStrong > SyncTolerance)
            {
                off++;
            }
        }

        return off / (double)notes.Count;
    }

    // circular distance between two phases in [0,1)
    private static double PhaseDist(double a, double b)
    {
        double d = Math.Abs(a - b);
        return Math.Min(d, 1.0 - d);
    }

    // --- Dexterity ---

    // How much the chart CHANGES vs loops, as distinct bar-patterns / total bars. Each bar is
    // signed by its notes quantised to 16 slots × voice; a song looping one bar reads low, a song of
    // varied grooves/fills reads high. A gentle difficulty modifier — a hard pattern stays hard even
    // when repeated (Everlong), so this only separates equally-hard charts by how much they vary.
    internal static int VarietySlots => 16;

    internal static double PatternVariety(IReadOnlyList<RoleNote> notes, SyncInfo sync)
    {
        if (notes.Count < 2 || sync.Resolution == 0 || sync.TimeSignatures.Count == 0)
        {
            return 0.0;
        }

        TimeSignatureSpan ts = sync.TimeSignatures[0];
        if (ts.Numerator <= 0 || ts.Denominator <= 0)
        {
            return 0.0;
        }

        double ticksPerBar = sync.Resolution * (4.0 / ts.Denominator) * ts.Numerator;
        if (ticksPerBar <= 0)
        {
            return 0.0;
        }

        var bars = new Dictionary<long, HashSet<int>>();
        foreach (RoleNote n in notes)
        {
            long bar = (long)(n.Tick / ticksPerBar);
            int slot = (int)Math.Round(n.Tick % ticksPerBar / ticksPerBar * VarietySlots) % VarietySlots;
            if (!bars.TryGetValue(bar, out HashSet<int>? cells))
            {
                cells = [];
                bars[bar] = cells;
            }

            _ = cells.Add(((int)n.Role * VarietySlots) + slot);
        }

        if (bars.Count < 2)
        {
            return 0.0;
        }

        var signatures = new HashSet<string>();
        foreach (HashSet<int> cells in bars.Values)
        {
            var sorted = cells.ToList();
            sorted.Sort();
            _ = signatures.Add(string.Join(",", sorted));
        }

        return signatures.Count / (double)bars.Count;
    }

    // Dexterity: breadth of kit coverage. Shannon entropy (bits) of the hit distribution across
    // distinct kit PIECES — bucketed by raw lane, so the three toms and both ride/crash cymbals count
    // as separate pieces (DrumRole collapses them and so cannot see a sweep across the toms). Kick is
    // excluded: it is a foot on a fixed pedal, not hand navigation. A chart concentrated on one piece
    // (single-tom pounding, a hi-hat-only groove) reads ~0; a fill ranging across snare, toms and
    // cymbals spreads its hits and approaches log2(pieces used). Raw bits are cached; the derivation
    // applies the ceiling, so the 0..10 mapping retunes without a rescan.
    internal static double KitPieceEntropy(IReadOnlyList<RoleNote> notes)
    {
        var counts = new Dictionary<int, int>();
        int total = 0;
        foreach (RoleNote n in notes)
        {
            if (n.Role == DrumRole.Kick)
            {
                continue;
            }

            counts[n.Lane] = counts.TryGetValue(n.Lane, out int c) ? c + 1 : 1;
            total++;
        }

        if (total == 0 || counts.Count < 2)
        {
            return 0.0;
        }

        double entropy = 0.0;
        foreach (int c in counts.Values)
        {
            double p = c / (double)total;
            entropy -= p * Math.Log(p) / Math.Log(2.0); // bits; Math.Log2 is absent on netstandard2.1
        }

        return entropy;
    }

    // --- Assembly ---
    public static DrumRawMetrics Measure(IReadOnlyList<RoleNote> notes, double durationSeconds, SyncInfo sync)
    {
        IndependenceRates ind = MeasureIndependence(notes, durationSeconds);
        return new(
            AvgNps: AvgNps(notes, durationSeconds),
            PeakNps: PeakNps(notes),
            LongestDenseSectionSeconds: LongestDenseSectionSeconds(notes, DenseThresholdNps),
            KickDensity: KickDensity(notes, durationSeconds),
            LongestKickRun: LongestKickRun(notes, KickRunMaxGap),
            PeakBurstNps: PeakBurstNps(notes),
            FastFillRate: FastFillRate(notes),
            ShortestTransitionGap: ShortestTransitionGap(notes),
            SyncopationFraction: SyncopationFraction(notes, sync.Resolution),
            OddMeterFraction: sync.OddMeterFraction(durationSeconds),
            PatternVariety: PatternVariety(notes, sync),
            OffCarrierPerSec: ind.OffCarrierPerSec,
            OffCarrierFastPerSec: ind.OffCarrierFastPerSec,
            ResidualAltPerSec: ind.ResidualAltPerSec,
            NoCarrierAltPerSec: ind.NoCarrierAltPerSec,
            FastestKickSpanNps: FastestKickSpanNps(notes),
            KitPieceEntropy: KitPieceEntropy(notes));
    }
}
