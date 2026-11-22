using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>An attribute's current value changed.</summary>
public readonly record struct AttributeChangedEvent(Entity Owner, Tag Attribute, float OldValue, float NewValue);
