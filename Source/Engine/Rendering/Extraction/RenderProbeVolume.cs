using System.Numerics;

using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>An irradiance volume and its center.</summary>
public readonly record struct RenderProbeVolume(IrradianceVolume Volume, Vector3 Center);
