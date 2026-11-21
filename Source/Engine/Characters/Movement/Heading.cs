using System.Numerics;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>The direction a character is steered along; the movement system adds it on a <see cref="SteerCommand"/> and removes it when steering ends.</summary>
public struct Heading
{
    public Vector3 Direction { get; set; }
}
