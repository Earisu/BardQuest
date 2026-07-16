namespace BardQuest.Domain.Progression;

/// <summary>A class node's standing on the journey path relative to the player's current class.</summary>
public enum ClassNodeState
{
    Cleared,
    Current,
    Locked,
}

/// <summary>One node on the journey path.</summary>
public sealed record ClassNode(PlayerClass Class, ClassNodeState State);

/// <summary>The ordered class ladder as journey nodes. Order is the canonical ascending prestige order
/// (the same order <see cref="ClassDerivation"/> bands on); classes below the player's are Cleared, the
/// player's is Current, higher ones are Locked.</summary>
public static class ClassLadder
{
    public static IReadOnlyList<PlayerClass> Order { get; } =
    [
        PlayerClass.Busker, PlayerClass.Minstrel, PlayerClass.Troubadour,
        PlayerClass.Bard, PlayerClass.Skald, PlayerClass.Legendweaver,
    ];

    public static IReadOnlyList<ClassNode> NodesFor(PlayerClass current)
    {
        int currentIndex = IndexOf(current);
        var nodes = new List<ClassNode>(Order.Count);
        for (int i = 0; i < Order.Count; i++)
        {
            ClassNodeState state = i < currentIndex ? ClassNodeState.Cleared
                : i == currentIndex ? ClassNodeState.Current
                : ClassNodeState.Locked;
            nodes.Add(new ClassNode(Order[i], state));
        }

        return nodes;
    }

    private static int IndexOf(PlayerClass cls)
    {
        for (int i = 0; i < Order.Count; i++)
        {
            if (Order[i] == cls)
            {
                return i;
            }
        }

        return 0;
    }
}
