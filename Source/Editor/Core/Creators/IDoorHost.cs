using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Editor.Core.Creators;

/// <summary>What the door tool needs of the creation's file: which parts are walls, the placed doors' fits, and the changes, each one undo step.</summary>
public interface IDoorHost
{
    /// <summary>Whether placed doors can be cut at all (Manifold's library is built).</summary>
    bool IsAvailable { get; }

    /// <summary>Whether the part is a wall a door can be placed on: not a door, and not a part that moves.</summary>
    bool IsWall(string partId);

    /// <summary>The fit of a placed door, or null for a part that is not one.</summary>
    DoorFit? FindFit(string doorId);

    /// <summary>Places a door and cuts its wall; the door's id, or null when it can't be made.</summary>
    string? Place(DoorFit fit);

    /// <summary>Gives a placed door new numbers and cuts its wall again.</summary>
    bool Replace(string doorId, DoorFit fit);

    /// <summary>Takes a placed door out and closes its wall.</summary>
    bool Remove(string doorId);
}
