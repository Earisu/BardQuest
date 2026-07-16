using BardQuest.Domain.Progression;

using Xunit;

namespace BardQuest.Domain.Tests.Progression;

public class ClassLadderTests
{
    [Fact]
    public void OrderIsTheCanonicalSixLadder()
        => Assert.Equal(
            [PlayerClass.Busker, PlayerClass.Minstrel, PlayerClass.Troubadour, PlayerClass.Bard, PlayerClass.Skald, PlayerClass.Legendweaver],
            ClassLadder.Order.ToArray());

    [Fact]
    public void NodesMarkBelowClearedCurrentCurrentAboveLocked()
    {
        IReadOnlyList<ClassNode> nodes = ClassLadder.NodesFor(PlayerClass.Troubadour);
        Assert.Equal(ClassNodeState.Cleared, nodes.Single(n => n.Class == PlayerClass.Busker).State);
        Assert.Equal(ClassNodeState.Cleared, nodes.Single(n => n.Class == PlayerClass.Minstrel).State);
        Assert.Equal(ClassNodeState.Current, nodes.Single(n => n.Class == PlayerClass.Troubadour).State);
        Assert.Equal(ClassNodeState.Locked, nodes.Single(n => n.Class == PlayerClass.Bard).State);
        Assert.Equal(ClassNodeState.Locked, nodes.Single(n => n.Class == PlayerClass.Legendweaver).State);
    }

    [Fact]
    public void FirstRunHasOnlyBuskerCurrentRestLocked()
    {
        IReadOnlyList<ClassNode> nodes = ClassLadder.NodesFor(PlayerClass.Busker);
        Assert.Equal(ClassNodeState.Current, nodes[0].State);
        Assert.All(nodes.Skip(1), n => Assert.Equal(ClassNodeState.Locked, n.State));
    }

    [Fact]
    public void LegendweaverHasEveryEarlierNodeCleared()
    {
        IReadOnlyList<ClassNode> nodes = ClassLadder.NodesFor(PlayerClass.Legendweaver);
        Assert.All(nodes.Take(5), n => Assert.Equal(ClassNodeState.Cleared, n.State));
        Assert.Equal(ClassNodeState.Current, nodes[5].State);
    }
}
