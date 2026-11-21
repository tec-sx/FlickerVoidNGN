using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Toggles;

/// <summary>Something an action opens and closes, such as a door or a lid; a locked <see cref="Locks.Lockable"/> on it keeps it as it is.</summary>
public struct Toggle
{
    /// <summary>The offer action that opens and closes it, such as <c>Interaction.Action.Toggle</c>.</summary>
    public Tag Action { get; set; }

    /// <summary>Whether it is open; a scene sets how it starts.</summary>
    public bool Open { get; set; }
}
