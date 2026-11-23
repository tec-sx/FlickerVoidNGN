using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Editor.Viewports;

/// <summary>The depth fades the viewport and its camera preview draw, and the point both are measured from.</summary>
internal readonly record struct ViewportDepthFade(DepthFade View, DepthFade Preview, Vector3 Focus);
