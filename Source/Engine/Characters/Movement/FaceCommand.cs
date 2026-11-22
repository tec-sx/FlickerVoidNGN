using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>Stops a character and turns it where it stands to face a point.</summary>
public readonly record struct FaceCommand(Entity Character, Vector3 Point);
