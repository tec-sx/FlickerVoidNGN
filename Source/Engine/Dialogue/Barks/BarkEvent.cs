using Arch.Core;

namespace FlickerVoid.Engine.Dialogue.Barks;

/// <summary>Sent when an entity says an ambient line, shown above it for <see cref="Duration"/> seconds.</summary>
public readonly record struct BarkEvent(Entity Speaker, string Text, float Duration);
