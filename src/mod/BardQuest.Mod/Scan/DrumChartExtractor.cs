extern alias yargpkg;
using System.Collections.Generic;

using yargpkg::YARG.Core;              // Difficulty — the runtime/package build, matching the loaded chart
using yargpkg::YARG.Core.Chart;        // SongChart, DrumNote, InstrumentTrack, FourLaneDrumPad

namespace BardQuest.Mod.Scan;

// Maps YARG's loaded ProDrums chart (kick + colored drums + cymbals) to BardQuest's neutral
// (time-seconds, lane-int) hit list, per charted difficulty. Lane = (int)FourLaneDrumPad, which
// matches BardQuest.Domain.Ratings.DrumPad ordinals (locked by DrumPadTests).
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

    public static IReadOnlyList<(double Time, int Lane)> Extract(
        SongChart chart, Difficulty difficulty, out double durationSeconds)
    {
        var hits = new List<(double Time, int Lane)>();
        InstrumentTrack<DrumNote> track = chart.ProDrums;
        if (!track.TryGetDifficulty(difficulty, out var diff) || diff == null)
        {
            durationSeconds = 0;
            return hits;
        }

        foreach (DrumNote parent in diff.Notes)
        {
            foreach (DrumNote n in parent.AllNotes) // parent + chord children
            {
                int lane = n.Pad;
                if (lane is < 0 or > 7)
                {
                    continue; // skip Wildcard (9) / anything outside our 0..7 vocabulary
                }

                hits.Add((n.Time, lane));
            }
        }

        hits.Sort((a, b) => a.Time.CompareTo(b.Time));
        durationSeconds = hits.Count > 0 ? hits[^1].Time - hits[0].Time : 0;
        return hits;
    }
}
