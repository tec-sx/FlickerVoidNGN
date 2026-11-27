using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Physics;

/// <summary>The physics simulation the game steps each fixed update, in engine types only, so the library behind it stays this module's detail.</summary>
public interface IPhysicsSystem : ISystem<GameTime>
{
    /// <summary>The number of bodies in the simulation, moving and static, asleep or awake.</summary>
    int BodyCount { get; }

    /// <summary>Whether the entity has a body in the simulation, which it gets in the first step after it has a rigid or static body.</summary>
    bool HasBody(Entity entity);

    /// <summary>Whether the entity's moving body is awake; a body at rest falls asleep until something touches or moves it, and static bodies never wake.</summary>
    bool IsAwake(Entity entity);

    /// <summary>How fast the entity's moving body travels, in metres a second; zero for an entity without one.</summary>
    Vector3 GetVelocity(Entity entity);

    /// <summary>The nearest body along the ray within <paramref name="maxDistance"/>, such as the floor or door the player clicked.</summary>
    bool RayCast(Ray ray, float maxDistance, out RayHit hit);

    /// <summary>The nearest body along the ray other than the ignored entity's, such as what a guard looking out from its own body sees.</summary>
    bool RayCast(Ray ray, float maxDistance, Entity ignored, out RayHit hit);

    /// <summary>
    /// Where a wheel of the entity's <see cref="WheeledBody"/> is now, in the entity's space: on its suspension, steered and turned
    /// as it rolled. False for an entity without a wheeled body in the simulation, or without that wheel.
    /// </summary>
    bool TryGetWheelPose(Entity entity, int wheel, out Transform pose);

    /// <summary>Whether a wheel of the entity's wheeled body touches something.</summary>
    bool IsWheelOnGround(Entity entity, int wheel);
}
