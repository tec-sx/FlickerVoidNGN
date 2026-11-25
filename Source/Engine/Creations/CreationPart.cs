using Arch.Core;

namespace FlickerVoid.Engine.Creations;

/// <summary>Marks an entity spawned as a part of a creation: the entity placed in the level that it belongs to, and its id in the creation; never saved.</summary>
public readonly record struct CreationPart(Entity Root, string Id);
