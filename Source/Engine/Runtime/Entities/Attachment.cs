using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>
/// How a nested entity hangs from its parent: the parent it found, where it stands in the parent's space and the world transform it
/// was last given, so a move of its own is told from the parent's; never saved, as it is worked out again from the transforms.
/// </summary>
internal struct Attachment
{
    public Entity Parent;
    public Transform Local;
    public Transform Applied;
}
