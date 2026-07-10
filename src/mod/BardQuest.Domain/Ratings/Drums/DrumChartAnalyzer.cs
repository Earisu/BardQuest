using YARG.Core;

namespace BardQuest.Domain.Ratings.Drums;

/// <summary>Thin adapter: maps neutral lane-ints to <see cref="DrumRole"/>s via its
/// <see cref="DrumKitMap"/>, then delegates to <see cref="DrumChartAnalysis.Measure"/>. Configured per
/// variant — today one instance for ProDrums with <see cref="DrumKitMap.ProFourLane"/>.</summary>
public sealed class DrumChartAnalyzer(Instrument instrument, DrumKitMap kit) : IChartAnalyzer
{
    private readonly DrumKitMap _kit = kit;

    public Instrument Instrument { get; } = instrument;

    public ChartMetrics Analyze(
        IReadOnlyList<(double Time, int Lane, uint Tick)> notes,
        double durationSeconds,
        int intensity,
        Difficulty difficulty,
        SyncInfo sync)
    {
        var roleNotes = new List<RoleNote>(notes.Count);
        foreach ((double time, int lane, uint tick) in notes)
        {
            DrumRole? role = _kit.Map(lane);
            if (role.HasValue)
            {
                roleNotes.Add(new RoleNote(time, role.Value, tick, lane));
            }
        }

        DrumRawMetrics raw = DrumChartAnalysis.Measure(roleNotes, durationSeconds, sync);
        return new ChartMetrics(Instrument, difficulty, intensity, raw);
    }
}
