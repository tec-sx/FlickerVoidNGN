namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>The attributes an entity has and their starting base values; while the game plays they are held by an <see cref="ActiveAttributeSet"/>.</summary>
public struct AttributeSet
{
    public AttributeSet()
    {
    }

    public List<AttributeEntry> Values { get; set; } = [];
}
