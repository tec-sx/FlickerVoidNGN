using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Definitions;

/// <summary>Data for one kind of thing (an item, a character, a faction, a scale), identified by a tag and extended through fragments.</summary>
public abstract class Definition
{
    /// <summary>Identifies the definition; definitions with state (scales, quests, factions) keep it in the fact this names.</summary>
    public Tag Id { get; init; }

    public DisplayInfo Display { get; init; } = new();

    public List<Tag> Tags { get; init; } = [];
}
