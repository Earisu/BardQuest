using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

namespace BardQuest.Domain.Quest;

/// <summary>The aggregate matcher — matches by RankScore (<see cref="AttributeProfile.Sum"/>) only;
/// per-axis targeting is a deliberate non-goal for now. It draws a working set at/just-above the player's
/// 0–50 score inside the current class band, and picks a class boss from the low end of the next band. All
/// constants are first-pass CALIBRATION TARGETS.</summary>
public static class MonsterMatcher
{
    public const int WorkingSetSize = 5;
    public const double CenterOffset = 2.0; // center just ABOVE the player's score
    public const double HalfWidth = 6.0;    // ± band around the center, on the 0–50 scale

    public static DeliveryWindow Window(double playerScore, PlayerClass band)
    {
        (double lo, double hi) = ClassDerivation.Range(band);
        double center = Math.Clamp(playerScore + CenterOffset, lo, hi);
        double wlo = Math.Max(lo, center - HalfWidth);
        double whi = Math.Min(hi, center + HalfWidth);
        return new DeliveryWindow(wlo, whi, center);
    }

    /// <summary>Up to <paramref name="size"/> in-window monsters nearest the center, excluding
    /// <paramref name="exclude"/>. Falls back to the nearest-to-center monsters library-wide if the
    /// window is too thin — never fails on a non-empty library.</summary>
    public static IReadOnlyList<string> WorkingSet(
        RatedLibrary library, DeliveryWindow w, int size, ISet<string> exclude)
    {
        var pool = library.Songs()
            .Where(s => !exclude.Contains(s.Hash))
            .Select(s => (s.Hash, s.Sum))
            .ToList();
        if (pool.Count == 0)
        {
            return [];
        }

        var inWindow = pool
            .Where(s => s.Sum >= w.Lo && s.Sum <= w.Hi)
            .OrderBy(s => Math.Abs(s.Sum - w.Center))
            .Select(s => s.Hash)
            .ToList();

        List<string> source = inWindow.Count >= size
            ? inWindow
            : [.. pool.OrderBy(s => Math.Abs(s.Sum - w.Center)).Select(s => s.Hash)];

        return source.Take(size).ToList();
    }

    /// <summary>A class boss for <paramref name="band"/>: the lowest monster whose RankScore reaches the
    /// next class's floor (<see cref="ClassDerivation.Range"/>.Hi), a fair step up. Null if the library
    /// has nothing that hard.</summary>
    public static string? PickBoss(RatedLibrary library, PlayerClass band, ISet<string> exclude)
    {
        (double _, double nextFloor) = ClassDerivation.Range(band);
        return library.Songs()
            .Where(s => s.Sum >= nextFloor && !exclude.Contains(s.Hash))
            .OrderBy(s => s.Sum)
            .Select(s => s.Hash)
            .FirstOrDefault();
    }
}
