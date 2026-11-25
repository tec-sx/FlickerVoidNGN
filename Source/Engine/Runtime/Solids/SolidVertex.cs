using System.Numerics;

namespace FlickerVoid.Engine.Runtime.Solids;

/// <summary>A corner of a solid's face as drawn: where it is, the face's normal, and its texture coordinates in metres.</summary>
public readonly record struct SolidVertex(Vector3 Position, Vector3 Normal, Vector2 TextureCoordinates);
