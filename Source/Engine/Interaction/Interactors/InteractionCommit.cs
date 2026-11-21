using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input.Gestures;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>An interaction under way on an interactor: what it uses, which offer (by index in the definition), and the gesture it follows.</summary>
public readonly record struct InteractionCommit(Entity Target, int Offer, Tag Input, Tag Action, GestureTracker Gesture);
