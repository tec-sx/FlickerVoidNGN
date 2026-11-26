using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// The element of a solid under the mouse: a point or a face by its number in <see cref="Index"/>, or an edge; a face also carries
/// the edge of it nearest the mouse, which a face loop runs across.
/// </summary>
public readonly record struct MeshHit(MeshElement Element, int Index, SolidEdge Edge)
{
    public static MeshHit OfPoint(int point) => new(MeshElement.Point, point, default);

    public static MeshHit OfEdge(SolidEdge edge) => new(MeshElement.Edge, -1, edge);

    public static MeshHit OfFace(int face, SolidEdge nearest) => new(MeshElement.Face, face, nearest);
}
