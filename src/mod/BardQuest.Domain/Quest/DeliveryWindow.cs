namespace BardQuest.Domain.Quest;

/// <summary>A 0–50 RankScore band the matcher draws from: [Lo, Hi] with a Center the working set
/// clusters around.</summary>
public readonly record struct DeliveryWindow(double Lo, double Hi, double Center);
