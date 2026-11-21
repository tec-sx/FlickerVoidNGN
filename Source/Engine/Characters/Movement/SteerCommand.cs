using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>
/// Walks a character along a direction on the ground, at its speed scaled by the direction's length (at most 1), until another
/// steer, a <see cref="MoveCommand"/> or a <see cref="StopCommand"/>; a zero direction stands it still. Keys and sticks steer.
/// </summary>
public readonly record struct SteerCommand(Entity Character, Vector3 Direction);
