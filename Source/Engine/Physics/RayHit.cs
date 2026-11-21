using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Physics;

/// <summary>Where a ray met a body: its entity, the point and surface normal, and how far along the ray.</summary>
public readonly record struct RayHit(Entity Entity, Vector3 Point, Vector3 Normal, float Distance);
