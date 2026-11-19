using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>The sphere a light reaches, in world space.</summary>
public readonly record struct LightBounds(Vector3 Position, float Range);
