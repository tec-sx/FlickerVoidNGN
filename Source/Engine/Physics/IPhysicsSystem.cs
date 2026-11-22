using Arch.Core;
using Arch.System;

using BepuPhysics;

using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Physics;

/// <summary>The physics simulation the game steps each fixed update; entities find their bodies in it by their <see cref="BodyHandle"/> or <see cref="StaticHandle"/> component.</summary>
public interface IPhysicsSystem : ISystem<GameTime>
{
    Simulation Simulation { get; }

    /// <summary>The nearest body along the ray within <paramref name="maxDistance"/>, such as the floor or door the player clicked.</summary>
    bool RayCast(Ray ray, float maxDistance, out RayHit hit);

    /// <summary>The nearest body along the ray other than the ignored entity's, such as what a guard looking out from its own body sees.</summary>
    bool RayCast(Ray ray, float maxDistance, Entity ignored, out RayHit hit);
}
