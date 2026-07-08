using BardQuest.Domain.Ratings;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings;

public class ChartAnalyzerTests
{
    private static (double Time, DrumPad Pad) Hit(double t, DrumPad pad) => (t, pad);

    [Fact]
    public void HasFastBass_ThreeConsecutiveKicksWithin300ms_ReturnsTrue()
    {
        (double Time, DrumPad Pad)[] hits = [Hit(0.0, DrumPad.Kick), Hit(0.1, DrumPad.Kick), Hit(0.2, DrumPad.Kick)
        ];
        Assert.True(ChartAnalyzer.HasFastBass(hits));
    }

    [Fact]
    public void HasFastBass_KicksMoreThan300msApart_ReturnsFalse()
    {
        (double Time, DrumPad Pad)[] hits = [Hit(0.0, DrumPad.Kick), Hit(0.5, DrumPad.Kick), Hit(1.0, DrumPad.Kick)
        ];
        Assert.False(ChartAnalyzer.HasFastBass(hits));
    }

    [Fact]
    public void HasIndependence_BassConsistentlyOffSnareGrid_ReturnsTrue()
    {
        (double Time, DrumPad Pad)[] hits =
        [
            Hit(0.00, DrumPad.RedDrum), Hit(0.25, DrumPad.Kick),
            Hit(0.50, DrumPad.RedDrum), Hit(0.75, DrumPad.Kick),
            Hit(1.00, DrumPad.RedDrum), Hit(1.25, DrumPad.Kick),
            Hit(1.50, DrumPad.RedDrum), Hit(1.75, DrumPad.Kick)
        ];
        Assert.True(ChartAnalyzer.HasIndependence(hits));
    }

    [Fact]
    public void HasIndependence_BassAlignedWithSnare_ReturnsFalse()
    {
        (double Time, DrumPad Pad)[] hits =
        [
            Hit(0.0, DrumPad.RedDrum), Hit(0.0, DrumPad.Kick),
            Hit(0.5, DrumPad.RedDrum), Hit(0.5, DrumPad.Kick),
            Hit(1.0, DrumPad.RedDrum), Hit(1.0, DrumPad.Kick)
        ];
        Assert.False(ChartAnalyzer.HasIndependence(hits));
    }

    [Fact]
    public void HasTomHeavy_MoreThan60PercentToms_ReturnsTrue()
    {
        (double Time, DrumPad Pad)[] hits =
        [
            Hit(0.0, DrumPad.YellowDrum), Hit(0.1, DrumPad.BlueDrum),
            Hit(0.2, DrumPad.GreenDrum), Hit(0.3, DrumPad.YellowCymbal)
        ];
        Assert.True(ChartAnalyzer.HasTomHeavy(hits));
    }

    [Fact]
    public void HasCymbalHeavy_MoreThan60PercentCymbals_ReturnsTrue()
    {
        (double Time, DrumPad Pad)[] hits =
        [
            Hit(0.0, DrumPad.YellowCymbal), Hit(0.1, DrumPad.BlueCymbal),
            Hit(0.2, DrumPad.GreenCymbal), Hit(0.3, DrumPad.YellowDrum)
        ];
        Assert.True(ChartAnalyzer.HasCymbalHeavy(hits));
    }

    [Fact]
    public void HasHighDensity_Above8NotesPerSecond_ReturnsTrue()
    {
        (double Time, DrumPad Pad)[] hits = [.. Enumerable.Range(0, 10).Select(i => Hit(i * 0.1, DrumPad.RedDrum))];
        Assert.True(ChartAnalyzer.HasHighDensity(hits, 8.0));
    }

    [Fact]
    public void HasHighDensity_Below8NotesPerSecond_ReturnsFalse()
    {
        (double Time, DrumPad Pad)[] hits = [.. Enumerable.Range(0, 4).Select(i => Hit(i * 0.25, DrumPad.RedDrum))];
        Assert.False(ChartAnalyzer.HasHighDensity(hits, 8.0));
    }

    [Fact]
    public void Analyze_FastBassPattern_ReturnsFastBassTag()
    {
        (double Time, DrumPad Pad)[] hits = [Hit(0.0, DrumPad.Kick), Hit(0.1, DrumPad.Kick), Hit(0.2, DrumPad.Kick)
        ];
        Assert.Contains("fast-bass", ChartAnalyzer.Analyze(hits, 8.0));
    }

    // Build a steady stream of `count` snare hits at `rate` notes/sec starting at t=0.
    private static List<(double Time, DrumPad Pad)> Stream(int count, double rate, double start = 0.0)
    {
        var list = new List<(double, DrumPad)>();
        for (int i = 0; i < count; i++)
        {
            list.Add((start + (i / rate), DrumPad.RedDrum));
        }

        return list;
    }

    [Fact]
    public void PeakNps_SteadyStream_EqualsTheStreamRate()
    {
        List<(double Time, DrumPad Pad)> hits = Stream(80, 8.0); // 8 notes/sec for 10s
        Assert.True(Math.Abs(ChartAnalyzer.PeakNps(hits) - 8.0) <= 0.5, $"expected ~8.0, got {ChartAnalyzer.PeakNps(hits)}");
    }

    [Fact]
    public void PeakNps_ShortBurstInQuietChart_ReflectsTheBurstNotTheAverage()
    {
        var hits = new List<(double, DrumPad)>();
        hits.AddRange(Stream(8, 2.0, 0.0)); // 0..4s
        hits.AddRange(Stream(27, 18.0, 4.0)); // 4..5.5s burst
        hits.AddRange(Stream(8, 2.0, 6.0)); // 6..10s
        Assert.True(Math.Abs(ChartAnalyzer.PeakNps(hits) - 18.0) <= 1.5, $"expected ~18.0, got {ChartAnalyzer.PeakNps(hits)}");
    }

    [Fact]
    public void PeakNps_SingleIsolatedFlam_DoesNotSpikeTheRating()
    {
        List<(double Time, DrumPad Pad)> hits = Stream(40, 4.0);
        hits.Add((5.00, DrumPad.YellowDrum));
        hits.Add((5.03, DrumPad.BlueDrum));
        Assert.True(ChartAnalyzer.PeakNps(hits) < 8.0);
    }

    [Fact]
    public void AvgNps_IsTotalNotesOverDuration()
    {
        List<(double Time, DrumPad Pad)> hits = Stream(50, 5.0); // 50 notes over 10s
        Assert.True(Math.Abs(ChartAnalyzer.AvgNps(hits, 10.0) - 5.0) <= 0.01, $"expected ~5.0, got {ChartAnalyzer.AvgNps(hits, 10.0)}");
    }

    [Fact]
    public void AvgNps_ZeroDuration_IsZero() => Assert.Equal(0.0, ChartAnalyzer.AvgNps(Stream(10, 5.0), 0.0));

    [Fact]
    public void Profile_EmptyHits_IsAllZero()
    {
        ChartDifficultyProfile p = ChartAnalyzer.Profile(new List<(double, DrumPad)>(), 0);
        Assert.Equal(0, p.PeakNps);
        Assert.Equal(0, p.AvgNps);
        Assert.Equal(0, p.DoubleBass);
        Assert.Equal(0, p.BlastBeat);
        Assert.Equal(0, p.Independence);
        Assert.Equal(0, p.FastFill);
    }

    [Fact]
    public void PeakNps_SingleNote_IsZero()
    {
        var hits = new List<(double, DrumPad)> { (0.0, DrumPad.RedDrum) };
        Assert.Equal(0.0, ChartAnalyzer.PeakNps(hits));
    }

    [Fact]
    public void AvgNps_NegativeDuration_IsZero() => Assert.Equal(0.0, ChartAnalyzer.AvgNps(Stream(10, 5.0), -3.0));

    private static List<(double Time, DrumPad Pad)> Kicks(int count, double rate, double start = 0.0)
    {
        var list = new List<(double, DrumPad)>();
        for (int i = 0; i < count; i++)
        {
            list.Add((start + (i / rate), DrumPad.Kick));
        }

        return list;
    }

    [Fact]
    public void DoubleBass_RelentlessFastKicks_RatesHigh() =>
        Assert.True(ChartAnalyzer.DoubleBass(Kicks(160, 16.0)) > 0.7);

    [Fact]
    public void DoubleBass_OccasionalSlowKicks_RatesLow() =>
        Assert.True(ChartAnalyzer.DoubleBass(Kicks(20, 2.0)) < 0.3);

    [Fact]
    public void BlastBeat_FastAlternatingSnareKick_RatesHigh()
    {
        var hits = new List<(double, DrumPad)>();
        for (int i = 0; i < 80; i++)
        {
            hits.Add((i / 16.0, i % 2 == 0 ? DrumPad.Kick : DrumPad.RedDrum));
        }

        Assert.True(ChartAnalyzer.BlastBeat(hits) > 0.6);
    }

    [Fact]
    public void BlastBeat_FastButAllKicks_RatesLow_NotABlast() =>
        Assert.True(ChartAnalyzer.BlastBeat(Kicks(80, 16.0)) < 0.3);

    [Fact]
    public void Independence_KicksOffTheHiHatGrid_RatesHigherThanOnGrid()
    {
        var onGrid = new List<(double, DrumPad)>();
        var offGrid = new List<(double, DrumPad)>();
        for (int i = 0; i < 20; i++)
        {
            double t = i / 4.0;
            onGrid.Add((t, DrumPad.YellowCymbal));
            offGrid.Add((t, DrumPad.YellowCymbal));
        }

        for (int i = 0; i < 20; i++)
        {
            double beat = i / 4.0;
            onGrid.Add((beat, DrumPad.Kick));
            offGrid.Add((beat + 0.125, DrumPad.Kick));
        }

        Assert.True(ChartAnalyzer.Independence(offGrid) > ChartAnalyzer.Independence(onGrid));
        Assert.True(ChartAnalyzer.Independence(offGrid) > 0.5);
    }

    [Fact]
    public void FastFill_DenseTomSnareBurst_RatesHigh()
    {
        var hits = new List<(double, DrumPad)>();
        DrumPad[] pads =
        [
            DrumPad.RedDrum, DrumPad.YellowDrum, DrumPad.BlueDrum, DrumPad.GreenDrum
        ];
        for (int i = 0; i < 24; i++)
        {
            hits.Add((i / 16.0, pads[i % pads.Length]));
        }

        Assert.True(ChartAnalyzer.FastFill(hits) > 0.6);
    }

    [Fact]
    public void FastFill_CymbalsAndKicksOnly_RatesLow()
    {
        var hits = new List<(double, DrumPad)>();
        for (int i = 0; i < 24; i++)
        {
            hits.Add((i / 16.0, i % 2 == 0 ? DrumPad.Kick : DrumPad.YellowCymbal));
        }

        Assert.True(ChartAnalyzer.FastFill(hits) < 0.3);
    }

    [Fact]
    public void DoubleBass_ShortBurstInLongSlowChart_RatesBelowRelentless()
    {
        var mixed = new List<(double, DrumPad)>();
        mixed.AddRange(Kicks(200, 2.0, 0.0)); // long slow section
        mixed.AddRange(Kicks(100, 12.0, 101.0)); // short fast burst
        List<(double Time, DrumPad Pad)> relentless = Kicks(100, 12.0);
        Assert.True(ChartAnalyzer.DoubleBass(mixed) < ChartAnalyzer.DoubleBass(relentless));
    }

    [Fact]
    public void BlastBeat_Gallop_RatesBelowStrictAlternation()
    {
        // Gallop K,K,S at 16/s vs strict K,S at 16/s — gallop alternates less, so it must score lower.
        var gallop = new List<(double, DrumPad)>();
        DrumPad[] pattern = [DrumPad.Kick, DrumPad.Kick, DrumPad.RedDrum];
        for (int i = 0; i < 90; i++)
        {
            gallop.Add((i / 16.0, pattern[i % 3]));
        }

        var strict = new List<(double, DrumPad)>();
        for (int i = 0; i < 90; i++)
        {
            strict.Add((i / 16.0, i % 2 == 0 ? DrumPad.Kick : DrumPad.RedDrum));
        }

        Assert.True(ChartAnalyzer.BlastBeat(gallop) < ChartAnalyzer.BlastBeat(strict));
    }

    [Fact]
    public void ProfileSorted_FieldsMatchThePublicDetectors_OnRepresentativeChart()
    {
        // A mixed ~6s chart: alternating kick/snare, toms, hi-hat + ride cymbals, and off-grid kicks —
        // enough for every detector to produce a non-trivial value.
        var hits = new List<(double Time, DrumPad Pad)>();
        for (int i = 0; i < 48; i++)
        {
            double t = i / 8.0; // 8 events/sec for 6s
            hits.Add((t, i % 2 == 0 ? DrumPad.Kick : DrumPad.RedDrum));
            hits.Add((t, DrumPad.YellowCymbal)); // steady hi-hat grid
            if (i % 4 == 0)
            {
                hits.Add((t + 0.06, DrumPad.Kick)); // off-grid kick (independence)
            }

            if (i % 3 == 0)
            {
                hits.Add((t, DrumPad.BlueDrum)); // toms (fast fills)
            }
        }

        const double duration = 6.0;
        var sorted = hits.OrderBy(h => h.Time).ToList();

        ChartDifficultyProfile p = ChartAnalyzer.ProfileSorted(sorted, duration);

        // Each field of the sorted (production) path must equal the public, independently-tested detector.
        Assert.Equal(ChartAnalyzer.PeakNps(hits), p.PeakNps);
        Assert.Equal(ChartAnalyzer.AvgNps(hits, duration), p.AvgNps);
        Assert.Equal(ChartAnalyzer.DoubleBass(hits), p.DoubleBass);
        Assert.Equal(ChartAnalyzer.BlastBeat(hits), p.BlastBeat);
        Assert.Equal(ChartAnalyzer.Independence(hits), p.Independence);
        Assert.Equal(ChartAnalyzer.FastFill(hits), p.FastFill);
    }
}
