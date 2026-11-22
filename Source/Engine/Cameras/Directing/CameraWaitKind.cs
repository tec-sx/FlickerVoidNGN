namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>What a <see cref="CameraWait"/> waits for.</summary>
internal enum CameraWaitKind
{
    Transition,
    ShotFinished,
    Marker,
}
