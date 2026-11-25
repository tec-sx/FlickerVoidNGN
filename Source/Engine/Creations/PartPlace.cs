using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Creations;

/// <summary>Where a part rests in the space of the entity placed in the level, as its kind last described it.</summary>
public readonly record struct PartPlace(Transform Place);
