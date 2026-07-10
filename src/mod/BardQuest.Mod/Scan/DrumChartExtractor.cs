extern alias yargpkg;

using BardQuest.Domain.Ratings;               // SyncInfo, TimeSignatureSpan

using yargpkg::YARG.Core;                      // Difficulty
using yargpkg::YARG.Core.Chart;                // SongChart, DrumNote, InstrumentTrack, SyncTrack, TimeSignatureChange

namespace BardQuest.Mod.Scan;

// Maps YARG's loaded ProDrums chart to BardQuest's neutral (time, lane, tick) note list per charted
// difficulty, plus a neutral SyncInfo (resolution + time-signature spans) for rhythmic metrics.
// Lane = (int)FourLaneDrumPad; DrumKitMap.ProFourLane interprets it.
public static class DrumChartExtractor
{
    private static readonly Difficulty[] Charted =
        [Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert];

    public static IReadOnlyList<Difficulty> AvailableDifficulties(SongChart chart)
    {
        var list = new List<Difficulty>(Charted.Length);
        InstrumentTrack<DrumNote> track = chart.ProDrums;
        foreach (Difficulty d in Charted)
        {
            if (track.TryGetDifficulty(d, out var diff) && diff != null && diff.Notes.Count > 0)
            {
                list.Add(d);
            }
        }

        return list;
    }

    public static SyncInfo BuildSyncInfo(SongChart chart)
    {
        SyncTrack sync = chart.SyncTrack;
        var spans = new List<TimeSignatureSpan>(sync.TimeSignatures.Count);
        foreach (TimeSignatureChange ts in sync.TimeSignatures)
        {
            spans.Add(new TimeSignatureSpan(sync.TickToTime(ts.Tick), (int)ts.Numerator, (int)ts.Denominator));
        }

        return new SyncInfo(sync.Resolution, spans);
    }

    public static IReadOnlyList<(double Time, int Lane, uint Tick)> Extract(
        SongChart chart, Difficulty difficulty, out double durationSeconds)
    {
        var notes = new List<(double Time, int Lane, uint Tick)>();
        InstrumentTrack<DrumNote> track = chart.ProDrums;
        if (!track.TryGetDifficulty(difficulty, out var diff) || diff == null)
        {
            durationSeconds = 0;
            return notes;
        }

        foreach (DrumNote parent in diff.Notes)
        {
            foreach (DrumNote n in parent.AllNotes) // parent + chord children
            {
                int lane = n.Pad;
                if (lane is < 0 or > 7)
                {
                    continue; // out of the ProFourLane vocabulary (Wildcard etc.)
                }

                notes.Add((n.Time, lane, n.Tick));
            }
        }

        notes.Sort((a, b) => a.Time.CompareTo(b.Time));
        durationSeconds = notes.Count > 0 ? notes[^1].Time - notes[0].Time : 0;
        return notes;
    }
}
