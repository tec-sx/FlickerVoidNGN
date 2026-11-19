using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>A point light and where it stands.</summary>
public readonly record struct RenderPointLight(PointLight Light, Vector3 Position);
