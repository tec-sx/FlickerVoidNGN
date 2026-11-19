using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>The probes the scene renderer lights a volume with this frame: the volume, where it is, and its probes, if any.</summary>
internal sealed record ProbeSource(IrradianceVolume Volume, Vector3 Center, IrradianceProbes? Probes);
