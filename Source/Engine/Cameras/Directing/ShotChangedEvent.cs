using Arch.Core;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>Sent when another shot goes live; <see cref="Previous"/> is <see cref="Entity.Null"/> for the first.</summary>
public readonly record struct ShotChangedEvent(Entity Previous, Entity Current);
