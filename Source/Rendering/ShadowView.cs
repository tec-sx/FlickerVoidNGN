using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>A view the shadow pass draws depth from into one atlas tile.</summary>
internal readonly record struct ShadowView(Matrix4x4 ViewProjection, ShadowTile Tile);
