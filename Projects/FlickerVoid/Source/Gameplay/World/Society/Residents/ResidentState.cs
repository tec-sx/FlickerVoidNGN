using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Gameplay.World.Society.Residents;

/// <summary>A resident while playing: its home, how long it rests yet, and whom it is watching.</summary>
public struct ResidentState
{
    public Vector3 Home { get; set; }

    public float RestLeft { get; set; }

    public Entity Watching { get; set; }
}
