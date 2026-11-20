using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Handles one fact's change; a removed fact reports 0 as its new value.</summary>
public delegate void FactChangedHandler(Tag tag, int oldValue, int newValue);
