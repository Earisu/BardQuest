namespace BardQuest.Domain.Ratings.Drums;

/// <summary>A drum note reduced to what measurement needs: onset time (seconds), its
/// <see cref="DrumRole"/>, its sync-grid <see cref="Tick"/> (for rhythmic Precision metrics), and the
/// raw <see cref="Lane"/> ordinal — the distinct kit piece, finer than <see cref="Role"/>, which
/// collapses the three toms and both ride/crash cymbals (Dexterity's kit-breadth metric needs the
/// pieces kept apart). Defaults to 0 for the many measurements that only care about role.</summary>
public readonly record struct RoleNote(double Time, DrumRole Role, uint Tick, int Lane = 0);
