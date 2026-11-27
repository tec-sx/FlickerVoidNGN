using FlickerVoid.Engine.Rendering.Instancing;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>A chunk entity's batch of copies, which the renderer reads but never changes.</summary>
public readonly record struct RenderInstanceBatch(int Id, InstanceBatch Batch);
