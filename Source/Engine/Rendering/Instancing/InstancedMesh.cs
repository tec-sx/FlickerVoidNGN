namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>
/// Draws a batch of copies of one model, standing where each copy says rather than at the entity's transform; a chunk entity
/// carries one, its copies are never entities. Systems add it at run time, so scenes never save it.
/// </summary>
public readonly record struct InstancedMesh(InstanceBatch Batch);
