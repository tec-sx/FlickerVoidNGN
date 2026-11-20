using Arch.System;

using BepuPhysics;

using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Physics;

/// <summary>The physics simulation the game steps each fixed update; entities find their bodies in it by their <see cref="BodyHandle"/> or <see cref="StaticHandle"/> component.</summary>
public interface IPhysicsSystem : ISystem<GameTime>
{
    Simulation Simulation { get; }
}
