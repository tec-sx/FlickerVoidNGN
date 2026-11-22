using System.Numerics;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>The way a character is turning to face where it stands, on the ground plane; removed once it faces it.</summary>
public struct Facing
{
    public Vector3 Direction { get; set; }
}
