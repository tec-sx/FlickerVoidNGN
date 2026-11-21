using Arch.Core;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>Stops a character where it stands.</summary>
public readonly record struct StopCommand(Entity Character);
