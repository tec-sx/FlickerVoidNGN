namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>What a save keeps of an entity's attributes: their base values and the modifiers on them.</summary>
public sealed class SavedAttributes
{
    public List<AttributeEntry> Values { get; set; } = [];

    public List<SavedModifier> Modifiers { get; set; } = [];
}
