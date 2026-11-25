using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>The entity this one is nested under, given as the entity rather than by name, as spawned parts are; never saved.</summary>
public readonly record struct NestedUnder(Entity Parent);
