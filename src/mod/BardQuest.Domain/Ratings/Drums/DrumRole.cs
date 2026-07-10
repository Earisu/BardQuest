namespace BardQuest.Domain.Ratings.Drums;

/// <summary>Instrument-variant-independent drum voice. The drum measurement runs entirely on roles;
/// a per-variant <see cref="DrumKitMap"/> assigns raw pad ordinals to these. HiHat is the timekeeping
/// cymbal (used by the independence grid); it also counts as a cymbal for cymbal-family checks.</summary>
public enum DrumRole
{
    Kick,
    Snare,
    Tom,
    Cymbal,
    HiHat,
}
