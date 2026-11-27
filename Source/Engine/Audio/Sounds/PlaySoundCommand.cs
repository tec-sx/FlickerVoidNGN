using System.Numerics;
using Arch.Core;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Asks for a sound to be played: on the entity when it is alive, else at the position when there is one, else as it is.</summary>
public readonly record struct PlaySoundCommand(string Sound, Vector3? Position = null, Entity Entity = default);
