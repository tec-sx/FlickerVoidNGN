using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>Sent when a live shot reaches a key with a marker, played or tracked, in either direction.</summary>
public readonly record struct ShotMarkerEvent(Entity Shot, Tag Marker, int Key);
