using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>
/// Sends a character walking to a point, replacing where it was going. <paramref name="Onto"/> finishes the walk straight onto the
/// point when paths stop short of it, as onto an elevator's platform standing in its shaft.
/// </summary>
public readonly record struct MoveCommand(Entity Character, Vector3 Point, bool Onto = false);
