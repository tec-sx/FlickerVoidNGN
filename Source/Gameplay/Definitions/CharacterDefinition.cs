namespace FlickerVoid.Engine.Gameplay.Definitions;

/// <summary>A character (player, named character or archetype), its feature data composed through fragments.</summary>
public sealed class CharacterDefinition : Definition
{
    public List<CharacterFragment> Fragments { get; init; } = [];

    public T? FindFragment<T>()
        where T : CharacterFragment
    {
        return Fragments.OfType<T>().FirstOrDefault();
    }
}
