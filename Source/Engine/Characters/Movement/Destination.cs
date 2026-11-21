using System.Numerics;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>Where a character is walking; the movement system adds it on a <see cref="MoveCommand"/> and removes it on arrival or a stop.</summary>
public struct Destination
{
    /// <summary>The point the character was sent to.</summary>
    public Vector3 Point { get; set; }

    /// <summary>The points it walks through, the last where it stops.</summary>
    public List<Vector3> Corners { get; set; }

    /// <summary>The corner it walks to now.</summary>
    public int Corner { get; set; }
}
