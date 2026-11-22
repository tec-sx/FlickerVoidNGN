using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>
/// A noise someone made at a point, heard by perceivers within their hearing range times its loudness and, when given, its range in
/// metres.
/// </summary>
public readonly record struct NoiseEvent(Entity Instigator, Vector3 Point, float Loudness = 1, float Range = 0);
