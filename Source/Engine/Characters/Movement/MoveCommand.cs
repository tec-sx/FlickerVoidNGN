using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>Sends a character walking to a point, replacing where it was going.</summary>
public readonly record struct MoveCommand(Entity Character, Vector3 Point);
