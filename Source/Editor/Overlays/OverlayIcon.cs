using System.Numerics;

namespace FlickerVoid.Engine.Editor.Overlays;

/// <summary>An icon the overlay draws facing the screen at a world position, tinted by a colour.</summary>
public readonly record struct OverlayIcon(Vector3 Position, EditorIcon Icon, Vector4 Color);
