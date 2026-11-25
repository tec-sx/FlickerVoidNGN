namespace FlickerVoid.Engine.Runtime.Solids;

/// <summary>A solid's faces as triangles: every face has its own corners, so its normal is flat, and each three indices are one triangle.</summary>
public sealed record SolidTriangles(SolidVertex[] Vertices, uint[] Indices);
