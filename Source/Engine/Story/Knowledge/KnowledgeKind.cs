namespace FlickerVoid.Engine.Story.Knowledge;

/// <summary>What sort of thing the protagonist knows.</summary>
public enum KnowledgeKind
{
    /// <summary>Evidence found while investigating; clues chain into deductions through prerequisites.</summary>
    Clue,

    /// <summary>Something the player can bring up in dialogue.</summary>
    Topic,

    /// <summary>A conclusion or idea the protagonist reaches.</summary>
    Thought,

    /// <summary>A read note, letter, book or map.</summary>
    Document,
}
