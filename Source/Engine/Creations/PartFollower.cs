namespace FlickerVoid.Engine.Creations;

/// <summary>
/// Makes a part of a creation stand in for another part of it, its leader: its joint opens and shuts with the leader's toggle, and
/// an interaction with it is done to the leader. The second leaf of a double door follows the first, and a pane of glass its leaf.
/// </summary>
public record struct PartFollower
{
    /// <summary>The leader's id in the creation.</summary>
    public string Leader { get; init; }
}
