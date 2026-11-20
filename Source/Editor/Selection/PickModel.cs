using System.Numerics;

namespace FlickerVoid.Engine.Editor.Selection;

/// <summary>A model's triangles in its own space, all parts together, and the box round them.</summary>
internal sealed record PickModel(Vector3[] Positions, uint[] Indices, Vector3 Min, Vector3 Max);
