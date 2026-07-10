namespace BardQuest.Domain.Ratings;

/// <summary>Neutral rhythm reference for a chart: pulse resolution (ticks per quarter note) plus the
/// ordered time-signature spans. Enough for Domain to classify syncopation/subdivision (from tick %
/// resolution) and odd meters, with no tempo map and no YARG types.</summary>
public sealed class SyncInfo(uint resolution, IReadOnlyList<TimeSignatureSpan> timeSignatures)
{
    public uint Resolution { get; } = resolution;

    public IReadOnlyList<TimeSignatureSpan> TimeSignatures { get; } = timeSignatures;

    /// <summary>Fraction of <paramref name="totalSeconds"/> spent in a signature other than 4/4.</summary>
    public double OddMeterFraction(double totalSeconds)
    {
        if (totalSeconds <= 0 || TimeSignatures.Count == 0)
        {
            return 0.0;
        }

        double odd = 0;
        for (int i = 0; i < TimeSignatures.Count; i++)
        {
            TimeSignatureSpan span = TimeSignatures[i];
            double end = i + 1 < TimeSignatures.Count ? TimeSignatures[i + 1].StartSeconds : totalSeconds;
            double dur = Math.Clamp(end, 0, totalSeconds) - Math.Clamp(span.StartSeconds, 0, totalSeconds);
            if (dur > 0 && !(span.Numerator == 4 && span.Denominator == 4))
            {
                odd += dur;
            }
        }

        return Math.Clamp(odd / totalSeconds, 0, 1);
    }
}
