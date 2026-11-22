using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Gameplay.Protagonist.Control;

/// <summary>Has the interactables its interactor can use within range outlined, so the player sees what can be clicked nearby.</summary>
public struct InteractableHighlight
{
    public InteractableHighlight()
    {
    }

    /// <summary>Metres from the interactor within which interactables are outlined.</summary>
    public float Range { get; set; } = 4;

    [Color]
    public Vector3 Color { get; set; } = new(1, 0.85f, 0.55f);
}
