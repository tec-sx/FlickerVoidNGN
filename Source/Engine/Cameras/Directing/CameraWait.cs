using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>Something waiting on the director: for its transition to end, a shot to finish playing or a shot to reach a marker.</summary>
internal sealed record CameraWait(object Owner, CameraWaitKind Kind, Entity Shot, Tag Marker, Action Done);
