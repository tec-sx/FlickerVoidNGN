namespace FlickerVoid.Engine.Attributes.Checks;

/// <summary>The dice a check rolls on top of the attribute and bonuses.</summary>
public enum CheckRoll : byte
{
    /// <summary>No roll; the attribute and bonuses decide alone.</summary>
    None,

    /// <summary>Two six-sided dice.</summary>
    D6x2,

    D10,

    D20,
}
