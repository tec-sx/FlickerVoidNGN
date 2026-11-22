namespace FlickerVoid.Engine.Attributes.Modifiers;

/// <summary>How a modifier changes an attribute: the current value is the base plus every Add, times every Multiply, unless an Override replaces it.</summary>
public enum ModifierOperation : byte
{
    Add,
    Multiply,
    Override,
}
