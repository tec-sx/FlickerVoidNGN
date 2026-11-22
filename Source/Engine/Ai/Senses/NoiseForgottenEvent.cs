using Arch.Core;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>A perceiver forgot the last noise it heard from an instigator.</summary>
public readonly record struct NoiseForgottenEvent(Entity Perceiver, Entity Instigator);
