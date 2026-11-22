using Arch.Core;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>A perceiver lost sight of a target: it went out of view, behind something or away.</summary>
public readonly record struct TargetLostEvent(Entity Perceiver, Entity Target);
