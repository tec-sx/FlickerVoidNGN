using System.Numerics;

namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>Where a camera is, where it looks and how far it zooms (orthographic height or field of view).</summary>
public readonly record struct ShotPose(Vector3 Position, Quaternion Rotation, float Zoom);
