using Arch.Core;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>A perceiver started seeing a target.</summary>
public readonly record struct TargetSeenEvent(Entity Perceiver, Entity Target);
