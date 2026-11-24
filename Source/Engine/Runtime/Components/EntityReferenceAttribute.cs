namespace FlickerVoid.Engine.Runtime.Components;

/// <summary>Marks a string property as the name of an entity of the same scene, so renaming that entity in the editor rewrites it and content checks find it.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EntityReferenceAttribute : Attribute;
