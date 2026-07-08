namespace BardQuest.Domain.Ratings;

/// <summary>
/// BardQuest's four-lane drum-pad vocabulary used by <see cref="ChartAnalyzer"/>. It deliberately
/// mirrors YARG.Core's <c>FourLaneDrumPad</c> (same members, same ordinal values) so the Mod's chart
/// extractor maps across with a plain cast — but keeping our own enum means the Domain's difficulty
/// analysis does not depend on the engine's chart-decoding types.
/// </summary>
public enum DrumPad
{
    Kick = 0,
    RedDrum = 1,
    YellowDrum = 2,
    BlueDrum = 3,
    GreenDrum = 4,
    YellowCymbal = 5,
    BlueCymbal = 6,
    GreenCymbal = 7,
}
