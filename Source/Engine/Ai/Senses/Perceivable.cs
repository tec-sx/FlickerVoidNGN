namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>An entity that perceivers can see, such as the protagonist.</summary>
public struct Perceivable
{
    public Perceivable()
    {
    }

    /// <summary>Metres above its feet where it is looked at.</summary>
    public float Height { get; set; } = 1;
}
