namespace FlickerVoid.Engine.Runtime.Components;

/// <summary>Marks a <see cref="System.Numerics.Vector3"/> property as a linear RGB colour, so the editor offers a colour picker.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ColorAttribute : Attribute;
