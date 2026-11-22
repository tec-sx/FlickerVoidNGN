using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>A perceiver heard a noise an instigator made at a point.</summary>
public readonly record struct NoiseHeardEvent(Entity Perceiver, Entity Instigator, Vector3 Point);
