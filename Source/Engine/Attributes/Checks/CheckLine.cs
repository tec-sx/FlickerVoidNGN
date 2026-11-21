namespace FlickerVoid.Engine.Attributes.Checks;

/// <summary>One part of a check's total, for the interface: the attribute, a bonus or the roll.</summary>
public readonly record struct CheckLine(string Label, int Value);
