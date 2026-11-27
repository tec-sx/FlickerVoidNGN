using System.Numerics;

namespace FlickerVoid.Engine.Interaction.Interactables;

/// <summary>
/// Where an interactable is used, in its entity's space, when that is not its origin: a door cut from a body keeps the body's pivot,
/// and is reached at its own middle.
/// </summary>
public struct UsePoint
{
    public Vector3 Offset;

    /// <summary>Whether the interactor stands on the point to use it, as on an elevator's platform, instead of within reach of it.</summary>
    public bool StandOn;
}
