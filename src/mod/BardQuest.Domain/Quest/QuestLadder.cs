using BardQuest.Domain.Progression;

namespace BardQuest.Domain.Quest;

/// <summary>The quest's 18-step ladder — 6 classes × 3 subranks — and the gate rules layered on B's
/// class ladder. A "step" is a linear index 0 (Busker I) … 17 (Legendweaver III). Within-class
/// boundaries are mini-bosses (accuracy gates whose bar escalates BY CLASS); the third boundary of a
/// class is the class boss (a difficulty gate). Bars are first-pass CALIBRATION TARGETS.</summary>
public static class QuestLadder
{
    public const int SubranksPerClass = ClassDerivation.SubranksPerClass; // 3
    public const int ClassCount = 6;
    public const int StepCount = ClassCount * SubranksPerClass; // 18
    public const int TopStep = StepCount - 1;                   // 17 = Legendweaver III

    /// <summary>The same-rank clear bar for a class's two mini-bosses — escalates by class.</summary>
    public static double MiniBossBar(PlayerClass cls) => cls switch
    {
        PlayerClass.Busker or PlayerClass.Minstrel => 0.85,
        PlayerClass.Troubadour or PlayerClass.Bard => 0.90,
        _ => 0.95, // Skald, Legendweaver
    };

    public static int StepIndex(PlayerClass cls, int subrank) => ((int)cls * SubranksPerClass) + subrank;

    public static PlayerClass ClassOfStep(int step) => (PlayerClass)(step / SubranksPerClass);

    public static int SubrankOfStep(int step) => step % SubranksPerClass;

    /// <summary>The ladder step of a 0–50 score, via B's <see cref="ClassDerivation.Derive"/>.</summary>
    public static int StepForScore(double score)
    {
        (PlayerClass cls, int sub) = ClassDerivation.Derive(score);
        return StepIndex(cls, sub);
    }

    /// <summary>True when the gate FROM <paramref name="step"/> (to step+1) crosses a class boundary —
    /// i.e. the step is a subrank III — making it a class boss rather than a mini-boss.</summary>
    public static bool IsClassBossGate(int step) => SubrankOfStep(step) == SubranksPerClass - 1;
}
