using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Editor.Overlays;

/// <summary>An icon the viewport shows for an entity: where, which and in what colour.</summary>
public readonly record struct IconInstance(Entity Entity, Vector3 Position, EditorIcon Icon, Vector4 Color);
