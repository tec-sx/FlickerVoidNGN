using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>Sent on the game's events when an action is pressed, released or cancelled, as gestures and interaction read it.</summary>
public readonly record struct InputActionEvent(Tag Action, InputPhase Phase);
