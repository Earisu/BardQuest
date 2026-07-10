using BardQuest.Domain.Ratings.Drums;

using Xunit;

namespace BardQuest.Domain.Tests.Ratings.Drums;

public class DrumChartAnalysisIndependenceTests
{
    private static DrumChartAnalysis.IndependenceRates Measure(IEnumerable<RoleNote> notes, double duration)
        => DrumChartAnalysis.MeasureIndependence([.. notes.OrderBy(n => n.Time)], duration);

    // 8th-note hi-hats at the given rate for the whole duration — a sustained carrier.
    private static IEnumerable<RoleNote> Hats(double rate, double duration)
    {
        for (double t = 0; t < duration; t += 1 / rate)
        {
            yield return new RoleNote(t, DrumRole.HiHat, 0u);
        }
    }

    [Fact]
    public void PlainBackbeat_ScoresNoIndependenceEvents()
    {
        // The classic false positive: hats + kick/snare all in unison with the carrier.
        var notes = new List<RoleNote>(Hats(4.0, 8.0));
        for (double t = 0; t < 8.0; t += 0.5)
        {
            notes.Add(new RoleNote(t, t % 1.0 < 0.25 ? DrumRole.Kick : DrumRole.Snare, 0u));
        }

        DrumChartAnalysis.IndependenceRates r = Measure(notes, 8.0);
        Assert.Equal(0.0, r.OffCarrierPerSec, 6); // the ostinato is seen, but unisons are not independence
        Assert.Equal(0.0, r.ResidualAltPerSec, 6);
        Assert.Equal(0.0, r.NoCarrierAltPerSec, 6);
    }

    [Fact]
    public void KicksBetweenFastHats_CountAsFastOffCarrier()
    {
        // The Everlong signature: a driving carrier with kicks landing BETWEEN its hits.
        var notes = new List<RoleNote>(Hats(6.0, 8.0));
        for (double t = 1.0 / 12.0; t < 8.0; t += 1.0)
        {
            notes.Add(new RoleNote(t, DrumRole.Kick, 0u)); // halfway between two hat hits
        }

        DrumChartAnalysis.IndependenceRates r = Measure(notes, 8.0);
        Assert.True(r.OffCarrierPerSec > 0.9);
        Assert.Equal(r.OffCarrierPerSec, r.OffCarrierFastPerSec, 6); // carrier IOI ~0.167 <= fast
    }

    [Fact]
    public void KicksBetweenSlowHats_AreOffCarrierButNotFast()
    {
        var notes = new List<RoleNote>(Hats(2.5, 8.0)); // IOI 0.4 — sustained but slow
        for (double t = 0.2; t < 8.0; t += 0.8)
        {
            notes.Add(new RoleNote(t, DrumRole.Kick, 0u));
        }

        DrumChartAnalysis.IndependenceRates r = Measure(notes, 8.0);
        Assert.True(r.OffCarrierPerSec > 0.9);
        Assert.Equal(0.0, r.OffCarrierFastPerSec, 6);
    }

    [Fact]
    public void BlastStyleAlternation_CountsResidualAlternations()
    {
        // 16th-note alternation of {hat+kick} / {snare} — carrier-stripped this is a fast K/S weave.
        var notes = new List<RoleNote>();
        for (int i = 0; i < 64; i++)
        {
            double t = i * 0.125;
            if (i % 2 == 0)
            {
                notes.Add(new RoleNote(t, DrumRole.HiHat, 0u));
                notes.Add(new RoleNote(t, DrumRole.Kick, 0u));
            }
            else
            {
                notes.Add(new RoleNote(t, DrumRole.Snare, 0u));
            }
        }

        DrumChartAnalysis.IndependenceRates r = Measure(notes, 8.0);
        Assert.True(r.ResidualAltPerSec > 5.0);
    }

    [Fact]
    public void CymbalFreeFill_CountsNoCarrierAlternations_ButAHatKickBeatDoesNot()
    {
        // A fast snare/tom/kick weave with no cymbals at all (a fill or solo).
        var fill = new List<RoleNote>();
        DrumRole[] cycle = [DrumRole.Snare, DrumRole.Tom, DrumRole.Kick];
        for (int i = 0; i < 40; i++)
        {
            fill.Add(new RoleNote(i * 0.1, cycle[i % 3], 0u));
        }

        Assert.True(Measure(fill, 4.0).NoCarrierAltPerSec > 5.0);

        // A fast hat/kick alternation whose hats are too sparse to be a carrier must NOT count —
        // that is the backbeat false positive sneaking back through the no-carrier branch.
        var beat = new List<RoleNote>();
        for (int i = 0; i < 10; i++)
        {
            beat.Add(new RoleNote(i * 1.0, DrumRole.HiHat, 0u));
            beat.Add(new RoleNote((i * 1.0) + 0.1, DrumRole.Kick, 0u));
        }

        Assert.Equal(0.0, Measure(beat, 10.0).NoCarrierAltPerSec, 6);
    }

    [Fact]
    public void EmptyOrDegenerate_IsAllZero()
    {
        Assert.Equal(default, Measure([], 0.0));
        Assert.Equal(default, Measure([new RoleNote(0.0, DrumRole.Kick, 0u)], 1.0));
    }
}
