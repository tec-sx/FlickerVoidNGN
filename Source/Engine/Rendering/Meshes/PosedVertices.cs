using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// The vertex buffer each part of an animated mesh is drawn from this frame and the one it was drawn from in the frame before:
/// the skinner's posed buffers for skinned parts, the part's own for the rest.
/// </summary>
internal readonly record struct PosedVertices(IBuffer[] Current, IBuffer[] Previous);
