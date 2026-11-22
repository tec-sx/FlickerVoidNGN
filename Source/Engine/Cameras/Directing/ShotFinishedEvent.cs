using Arch.Core;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>Sent when a played shot reaches the key it was played to.</summary>
public readonly record struct ShotFinishedEvent(Entity Shot);
