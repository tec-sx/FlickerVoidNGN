namespace FlickerVoid.Engine.Attributes.Modifiers;

/// <summary>A modifier on an entity, with the handle that takes it off again.</summary>
public readonly record struct AppliedModifier(Guid Handle, ModifierDefinition Modifier);
