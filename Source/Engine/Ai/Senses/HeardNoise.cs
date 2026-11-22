using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>The last noise a perceiver heard from an instigator, where it was and how many seconds ago.</summary>
public readonly record struct HeardNoise(Entity Instigator, Vector3 Point, float Age);
