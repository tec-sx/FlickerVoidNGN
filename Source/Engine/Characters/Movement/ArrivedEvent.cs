using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>Sent when a character reaches its destination.</summary>
public readonly record struct ArrivedEvent(Entity Character, Vector3 Point);
