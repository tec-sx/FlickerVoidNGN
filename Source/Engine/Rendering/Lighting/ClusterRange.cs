using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>Where a cluster's lights sit in <see cref="LightClusters.Indices"/>, as the scene shader reads it: 8 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ClusterRange(uint Offset, uint Count);

