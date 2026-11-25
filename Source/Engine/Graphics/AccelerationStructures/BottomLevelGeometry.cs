using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Graphics.AccelerationStructures;

/// <summary>
/// The triangles a bottom-level acceleration structure is built from: vertices with a float3 position at their start, every
/// <paramref name="VertexStride"/> bytes, and 32-bit indices, three a triangle. Both buffers need
/// <see cref="BufferUsage.AccelerationStructureInput"/>.
/// </summary>
public readonly record struct BottomLevelGeometry(IBuffer Vertices, int VertexCount, int VertexStride, IBuffer Indices, int IndexCount);
