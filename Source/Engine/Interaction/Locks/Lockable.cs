using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Locks;

/// <summary>A lock on an interactable, such as a door's: whether it is locked, the action that locks and unlocks it, and how it can be opened.</summary>
public struct Lockable
{
    public Lockable()
    {
    }

    public bool Locked { get; set; } = true;

    /// <summary>The offer action that locks it when open and unlocks it when locked, such as <c>Interaction.Action.Unlock</c>.</summary>
    public Tag Action { get; set; }

    /// <summary>Whether it can be picked.</summary>
    public bool Pickable { get; set; } = true;

    /// <summary>How hard it is to pick, from 0 to 1.</summary>
    public float Difficulty { get; set; } = 0.5f;

    /// <summary>A tag an item's definition must have to open it; none when no key fits.</summary>
    public Tag Key { get; set; }

    /// <summary>Whether the key is used up when it opens the lock.</summary>
    public bool ConsumeKey { get; set; }
}
