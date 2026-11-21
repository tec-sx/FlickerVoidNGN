using System.Numerics;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>Where a character is walking; the movement system adds it on a <see cref="MoveCommand"/> and removes it on arrival or a stop.</summary>
public struct Destination
{
    public Vector3 Point { get; set; }
}
