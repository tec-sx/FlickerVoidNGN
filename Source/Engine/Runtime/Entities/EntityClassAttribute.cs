namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>
/// Marks a <c>partial struct</c> as an entity class: each of its <c>partial ref</c> properties is a component its entities have, and
/// the generator turns it into a typed handle over an entity (<see cref="IEntityClass{TSelf}"/>), so <c>door.Lock.IsLocked</c> reads
/// the entity's own component.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class EntityClassAttribute : Attribute
{
    /// <summary>The entity template (<c>.fventity</c>) entities of the class spawn from, relative to the content folder, if any.</summary>
    public string? Template { get; set; }
}
