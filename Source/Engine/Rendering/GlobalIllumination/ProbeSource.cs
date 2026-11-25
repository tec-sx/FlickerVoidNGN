using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Rendering.GlobalIllumination;

/// <summary>
/// The probes the scene renderer lights a volume with this frame: the volume, where it is, and its baked probes, or the buffer of
/// the probes traced live on the GPU (as <see cref="IrradianceProbes.WriteTo"/> lays them out).
/// </summary>
internal sealed record ProbeSource(IrradianceVolume Volume, Vector3 Center, IrradianceProbes? Probes, IBuffer? Live = null);
