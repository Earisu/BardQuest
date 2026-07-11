namespace BardQuest.Domain.Progression;

/// <summary>Maps cumulative per-axis XP to a level 0–<see cref="MaxLevel"/>. The cost to REACH level L
/// (from L-1) is <see cref="XpScale"/> × Base × L^<see cref="Exp"/> — gently steeper toward the top —
/// where Base is pace-scaled. <see cref="XpScale"/> is COSMETIC: applied identically here and to the
/// XP awards (see <see cref="AttributeXpFormula"/>), it changes only on-screen magnitude, never the
/// songs-per-level pacing (that is Base). All constants are first-pass CALIBRATION TARGETS.</summary>
public sealed class LevelCurve
{
    public const int MaxLevel = 10;
    public const double Exp = 1.3;          // summit steepness (p): gently steeper than linear
    public const double XpScale = 100.0;    // cosmetic — bigger numbers, identical pacing

    // Base = the cost-to-earnings ratio, i.e. songs per level. THIS is the pacing dial (not cosmetic).
    public const double SprintBase = 2.4, JourneyBase = 3.6, OdysseyBase = 5.4;

    // _cumulative[L] = XP required to be AT level L (index 0..MaxLevel; [0] = 0).
    private readonly double[] _cumulative;

    private LevelCurve(double baseCost)
    {
        _cumulative = new double[MaxLevel + 1];
        double running = 0;
        for (int level = 1; level <= MaxLevel; level++)
        {
            running += XpScale * baseCost * Math.Pow(level, Exp); // cost to reach `level` from level-1
            _cumulative[level] = running;
        }
    }

    public static LevelCurve ForPace(QuestPace pace) => new(pace switch
    {
        QuestPace.Sprint => SprintBase,
        QuestPace.Odyssey => OdysseyBase,
        _ => JourneyBase,
    });

    /// <summary>Highest level (0..<see cref="MaxLevel"/>) whose cumulative cost <paramref name="xp"/> meets.</summary>
    public int LevelFor(double xp)
    {
        int level = 0;
        while (level < MaxLevel && xp >= _cumulative[level + 1])
        {
            level++;
        }

        return level;
    }

    /// <summary>Cumulative XP required to be at <paramref name="level"/> (0 at level 0).</summary>
    public double CumulativeXpFor(int level) => _cumulative[Math.Clamp(level, 0, MaxLevel)];

    /// <summary>Progress within the current level: (level, xpIntoLevel, xpNeededForThisLevel). At
    /// <see cref="MaxLevel"/>, Needed is 0.</summary>
    public (int Level, double Into, double Needed) Progress(double xp)
    {
        int level = LevelFor(xp);
        if (level >= MaxLevel)
        {
            return (MaxLevel, 0, 0);
        }

        double floor = _cumulative[level];
        double ceil = _cumulative[level + 1];
        return (level, xp - floor, ceil - floor);
    }
}
