using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>A fog volume placed in the world: its entity's position (the bottom of its box) and turn, with the volume itself.</summary>
public readonly record struct RenderFogVolume(Vector3 Position, Quaternion Rotation, FogVolume Volume);
