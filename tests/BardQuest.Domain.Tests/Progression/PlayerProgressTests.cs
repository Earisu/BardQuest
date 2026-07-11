using BardQuest.Domain.Progression;
using BardQuest.Domain.Ratings;

using YARG.Core;

using Xunit;

namespace BardQuest.Domain.Tests.Progression;

public class PlayerProgressTests
{
    private static PerformanceFacts Cleared(double percent = 1.0)
        => new(percent, IsFc: false, Stars: 5, NotesHit: 100, NotesMissed: 0, Difficulty.Expert);

    private static AttributeProfile Song(double strength, double technique)
        => new(new Dictionary<Attribute, double>
        {
            [Attribute.Strength] = strength,
            [Attribute.Technique] = technique,
        });

    private static (AttributeProfile, PerformanceFacts) Play(double strength, double technique, double percent = 1.0)
        => (Song(strength, technique), Cleared(percent));

    [Fact]
    public void EmptyQuestIsBuskerWithNoXp()
    {
        PlayerProfile profile = PlayerProgress.Build([], QuestPace.Journey);
        Assert.Equal(0.0, profile.Score);
        Assert.Equal(PlayerClass.Busker, profile.Class);
        Assert.Equal(0.0, profile[Attribute.Strength].Xp);
    }

    [Fact]
    public void CompletingSongsGrantsXpAndLevels()
    {
        // A handful of strength-heavy expert clears should raise Strength above 0.
        var plays = Enumerable.Repeat(Play(strength: 8, technique: 1), 10).ToList();
        PlayerProfile profile = PlayerProgress.Build(plays, QuestPace.Journey);
        Assert.True(profile[Attribute.Strength].Level > 0);
        Assert.True(profile[Attribute.Strength].Xp > 0);
    }

    [Fact]
    public void SpecializationEmergesFromWhatYouPlay()
    {
        var plays = Enumerable.Repeat(Play(strength: 8, technique: 1), 20).ToList();
        PlayerProfile profile = PlayerProgress.Build(plays, QuestPace.Journey);
        Assert.True(profile[Attribute.Strength].Level > profile[Attribute.Technique].Level);
    }

    [Fact]
    public void OutleveledRerunsAddNothing()
    {
        // Build up Strength on demand-8 songs, then a demand-2 song adds no Strength XP once outgrown.
        var climb = Enumerable.Repeat(Play(strength: 8, technique: 0), 40).ToList();
        PlayerProfile before = PlayerProgress.Build(climb, QuestPace.Journey);

        var withRerun = new List<(AttributeProfile, PerformanceFacts)>(climb) { Play(strength: 2, technique: 0) };
        PlayerProfile after = PlayerProgress.Build(withRerun, QuestPace.Journey);

        // Strength is already ≥ 5 (over ≥ 3 vs demand 2 → gap penalty 0), so XP is unchanged.
        Assert.True(before[Attribute.Strength].Level >= 5);
        Assert.Equal(before[Attribute.Strength].Xp, after[Attribute.Strength].Xp, 6);
    }

    [Fact]
    public void BuildIsDeterministic()
    {
        var plays = Enumerable.Repeat(Play(strength: 6, technique: 3), 12).ToList();
        PlayerProfile a = PlayerProgress.Build(plays, QuestPace.Journey);
        PlayerProfile b = PlayerProgress.Build(plays, QuestPace.Journey);
        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a[Attribute.Strength].Xp, b[Attribute.Strength].Xp, 6);
    }
}
