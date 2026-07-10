namespace BardQuest.Domain.Ratings;

/// <summary>One time-signature region, starting at <paramref name="StartSeconds"/> and running until
/// the next span (or the chart end).</summary>
public readonly record struct TimeSignatureSpan(double StartSeconds, int Numerator, int Denominator);
