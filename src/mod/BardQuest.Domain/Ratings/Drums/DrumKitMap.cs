namespace BardQuest.Domain.Ratings.Drums;

/// <summary>Maps one drum variant's raw pad ordinals to <see cref="DrumRole"/>s, so the single drum
/// measurement algorithm serves pro / 4-lane / 5-lane. Only <see cref="ProFourLane"/> exists today;
/// future variants are added here without touching the algorithm.</summary>
public sealed class DrumKitMap
{
    private readonly IReadOnlyDictionary<int, DrumRole> _byLane;

    private DrumKitMap(IReadOnlyDictionary<int, DrumRole> byLane)
    {
        _byLane = byLane;
    }

    /// <summary>Rock Band Pro Drums / four-lane-with-cymbals. Lane = (int)FourLaneDrumPad.</summary>
    public static DrumKitMap ProFourLane { get; } = new(new Dictionary<int, DrumRole>
    {
        [0] = DrumRole.Kick,    // Kick
        [1] = DrumRole.Snare,   // RedDrum
        [2] = DrumRole.Tom,     // YellowDrum
        [3] = DrumRole.Tom,     // BlueDrum
        [4] = DrumRole.Tom,     // GreenDrum
        [5] = DrumRole.HiHat,   // YellowCymbal
        [6] = DrumRole.Cymbal,  // BlueCymbal
        [7] = DrumRole.Cymbal,  // GreenCymbal
    });

    /// <summary>The role for a raw lane ordinal, or null to drop the note (out of vocabulary).</summary>
    public DrumRole? Map(int lane) => _byLane.TryGetValue(lane, out DrumRole r) ? r : null;

    public static bool IsCymbalFamily(DrumRole r) => r is DrumRole.Cymbal or DrumRole.HiHat;
}
