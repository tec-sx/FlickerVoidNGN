using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>An indoor volume's box in world space, the share of ambient light inside it, and how far it mutes the world outside while it is the cutaway.</summary>
public readonly record struct RenderIndoor(Vector3 Min, Vector3 Max, float Ambient, float OutsideMuted);
