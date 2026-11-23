namespace FlickerVoid.Engine.Runtime.Components;

/// <summary>The entity, by its name in the scene, this entity is nested under; its transform follows the parent's.</summary>
public readonly record struct EntityParent(string Name);
