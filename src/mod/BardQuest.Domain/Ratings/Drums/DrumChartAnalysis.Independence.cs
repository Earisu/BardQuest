namespace BardQuest.Domain.Ratings.Drums;

/// <summary>Limb-independence measurement (the Technique raw metrics). The core idea: find the
/// sustained cymbal ostinato (the "carrier" — the timekeeping hand), strip it, and count what the
/// OTHER limbs genuinely do against it. This dodges the continuous-ostinato trap that sinks naive
/// voice-set checks: under a steady hi-hat every onset group contains hi-hat, so set-*changes*
/// over-fire on plain backbeats while disjoint-set checks read zero on exactly the hard charts.</summary>
public static partial class DrumChartAnalysis
{
    // --- Technique: independence events against the timekeeping ostinato ---
    public const double CarrierMaxIoi = 0.45; // ostinato hits at/below this spacing count as sustained
    public const int CarrierMinRun = 8;       // minimum consecutive hits for a run to be an ostinato
    public const double FastCarrierIoi = 0.20; // carrier at/above 5 hits/s is "fast" (driving 16ths)
    public const double SimEpsilon = 0.01;     // seconds within which onsets collapse to one simultaneous group
    public const double CoordFastGap = 0.16;   // seconds; successive onset groups closer than this are "fast"

    /// <summary>The independence event rates. All are events **per second** — rates are intrinsically
    /// speed-weighted, so a slow jazzy weave reads low without any extra gate. Cached individually so
    /// the Technique mix stays derive-tunable.</summary>
    internal readonly record struct IndependenceRates(
        double OffCarrierPerSec,     // figure onsets BETWEEN ostinato hits (limb subdividing alone)
        double OffCarrierFastPerSec, // same, only under a fast ostinato (the Everlong signature)
        double ResidualAltPerSec,    // fast carrier-stripped voice alternations (blasts, weaves)
        double NoCarrierAltPerSec);  // fast disjoint alternations between cymbal-free groups (fills)

    internal static IndependenceRates MeasureIndependence(IReadOnlyList<RoleNote> notes, double duration)
    {
        if (notes.Count < 2 || duration <= 0)
        {
            return default;
        }

        List<(double Time, HashSet<DrumRole> Roles)> groups = OnsetGroups(notes);
        List<CarrierSpan> spans = CarrierSpans(notes);

        int offCarrier = 0, offCarrierFast = 0, residualAlt = 0, noCarrierAlt = 0;
        HashSet<DrumRole>? prevResidual = null;
        bool prevUnderCarrier = false, prevCymbalFree = false;
        double prevTime = double.NegativeInfinity;

        foreach ((double time, HashSet<DrumRole> roles) in groups)
        {
            CarrierSpan? carrier = Covering(spans, time);
            var residual = new HashSet<DrumRole>(roles);
            if (carrier != null)
            {
                _ = residual.Remove(carrier.Role);
            }

            // A figure limb striking BETWEEN carrier hits must subdivide on its own — true
            // independence. Striking together with a carrier hit (unison) is not counted here.
            if (carrier != null && residual.Count > 0 && !roles.Contains(carrier.Role))
            {
                offCarrier++;
                if (carrier.MedianIoi <= FastCarrierIoi)
                {
                    offCarrierFast++;
                }
            }

            bool cymbalFree = !roles.Contains(DrumRole.HiHat) && !roles.Contains(DrumRole.Cymbal);
            if (time - prevTime <= CoordFastGap && prevResidual != null)
            {
                if (carrier != null && prevUnderCarrier)
                {
                    // Carrier-stripped alternation: {HH+K} -> {HH+S} is a real K->S interleave;
                    // {HH} -> {HH+K} (a limb tapping along) strips to {} -> {K} and does not count.
                    if (residual.Count > 0 && prevResidual.Count > 0 && !residual.SetEquals(prevResidual))
                    {
                        residualAlt++;
                    }
                }
                else if (carrier == null && !prevUnderCarrier
                    && cymbalFree && prevCymbalFree
                    && roles.Count > 0 && prevResidual.Count > 0 && !roles.Overlaps(prevResidual))
                {
                    // Fast weaves among the non-timekeeping limbs (fills, solos). Requiring BOTH
                    // groups cymbal-free keeps a plain beat that merely lacks a sustained ostinato
                    // from re-admitting the backbeat false positive.
                    noCarrierAlt++;
                }
            }

            prevResidual = carrier == null ? roles : residual;
            prevUnderCarrier = carrier != null;
            prevCymbalFree = cymbalFree;
            prevTime = time;
        }

        return new IndependenceRates(
            OffCarrierPerSec: offCarrier / duration,
            OffCarrierFastPerSec: offCarrierFast / duration,
            ResidualAltPerSec: residualAlt / duration,
            NoCarrierAltPerSec: noCarrierAlt / duration);
    }

    private sealed record CarrierSpan(double Start, double End, double MedianIoi, DrumRole Role);

    // Sustained quasi-continuous runs of one cymbal-family voice (the timekeeping ostinato).
    private static List<CarrierSpan> CarrierSpans(IReadOnlyList<RoleNote> notes)
    {
        var spans = new List<CarrierSpan>();
        foreach (DrumRole role in (DrumRole[])[DrumRole.HiHat, DrumRole.Cymbal])
        {
            var times = new List<double>();
            foreach (RoleNote n in notes)
            {
                if (n.Role == role)
                {
                    times.Add(n.Time);
                }
            }

            int start = 0;
            for (int i = 1; i <= times.Count; i++)
            {
                if (i < times.Count && times[i] - times[i - 1] <= CarrierMaxIoi)
                {
                    continue;
                }

                if (i - start >= CarrierMinRun)
                {
                    var iois = new List<double>(i - start - 1);
                    for (int j = start + 1; j < i; j++)
                    {
                        iois.Add(times[j] - times[j - 1]);
                    }

                    iois.Sort();
                    spans.Add(new CarrierSpan(times[start], times[i - 1], iois[iois.Count / 2], role));
                }

                start = i;
            }
        }

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    // The span covering t; when hi-hat and ride ostinatos overlap, the faster one wins.
    private static CarrierSpan? Covering(List<CarrierSpan> spans, double t)
    {
        CarrierSpan? best = null;
        foreach (CarrierSpan s in spans)
        {
            if (s.Start - SimEpsilon > t)
            {
                break; // sorted by start; nothing later can cover t
            }

            if (t <= s.End + SimEpsilon && (best == null || s.MedianIoi < best.MedianIoi))
            {
                best = s;
            }
        }

        return best;
    }

    // Onsets within SimEpsilon collapse to one group of simultaneous voices.
    private static List<(double Time, HashSet<DrumRole> Roles)> OnsetGroups(IReadOnlyList<RoleNote> notes)
    {
        var groups = new List<(double, HashSet<DrumRole>)>();
        int g = 0;
        while (g < notes.Count)
        {
            double t0 = notes[g].Time;
            var roles = new HashSet<DrumRole>();
            int h = g;
            while (h < notes.Count && notes[h].Time - t0 <= SimEpsilon)
            {
                _ = roles.Add(notes[h].Role);
                h++;
            }

            groups.Add((t0, roles));
            g = h;
        }

        return groups;
    }
}
