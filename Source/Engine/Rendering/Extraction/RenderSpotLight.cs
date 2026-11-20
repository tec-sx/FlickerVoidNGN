using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>A spot light, where it stands and where it points.</summary>
public readonly record struct RenderSpotLight(SpotLight Light, Vector3 Position, Quaternion Rotation);
