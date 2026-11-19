using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>A camera and where it stands.</summary>
public readonly record struct RenderCamera(Camera Camera, Transform Transform);
