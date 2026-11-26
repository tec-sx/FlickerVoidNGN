using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// The points, edges and faces selected in a solid while its mesh is edited, as in Blender: clicks select elements of the current
/// kind, and the other kinds follow from them: a point mode selection holds the edges and faces whose points are all selected, an
/// edge mode one the points of its edges and the faces whose edges are all selected, a face mode one its edges and points.
/// Changing the kind keeps what follows from the old selection.
/// </summary>
public sealed class MeshSelection
{
    private SolidMesh _solid;
    private SolidTopology _topology;
    private readonly HashSet<int> _points = [];
    private readonly HashSet<SolidEdge> _edges = [];
    private readonly HashSet<int> _faces = [];

    public MeshSelection(SolidMesh solid)
    {
        _solid = solid;
        _topology = new SolidTopology(solid);
    }

    /// <summary>The kind of element a click selects.</summary>
    public MeshElement Element { get; private set; }

    public IReadOnlySet<int> Points => _points;

    public IReadOnlySet<SolidEdge> Edges => _edges;

    public IReadOnlySet<int> Faces => _faces;

    /// <summary>
    /// Moves the selection to another copy of the solid, such as one with its points moved, when it has the same faces; returns
    /// false, leaving the selection as it is, for a solid with others.
    /// </summary>
    public bool TryFollow(SolidMesh solid)
    {
        if (solid.Points.Count != _solid.Points.Count || solid.Faces.Count != _solid.Faces.Count)
        {
            return false;
        }

        for (int face = 0; face < solid.Faces.Count; face++)
        {
            if (!solid.Faces[face].SequenceEqual(_solid.Faces[face]))
            {
                return false;
            }
        }

        _solid = solid;
        _topology = new SolidTopology(solid);
        return true;
    }

    /// <summary>Makes clicks select this kind of element, keeping what follows from the selection so far.</summary>
    public void SetElement(MeshElement element)
    {
        Element = element;
        Flush();
    }

    /// <summary>
    /// Selects the element hit, replacing the selection, or toggling it in the selection when <paramref name="additive"/> (Shift);
    /// nothing hit clears the selection, unless additive.
    /// </summary>
    public void Select(MeshHit? hit, bool additive)
    {
        if (hit is not { } found)
        {
            if (!additive)
            {
                Clear();
            }

            return;
        }

        if (!additive)
        {
            ClearPrimary();
        }

        switch (found.Element)
        {
            case MeshElement.Point:
                Toggle(_points, found.Index, additive);
                break;
            case MeshElement.Edge:
                Toggle(_edges, found.Edge, additive);
                break;
            default:
                Toggle(_faces, found.Index, additive);
                break;
        }

        Flush();
    }

    /// <summary>
    /// Selects the loop through the element hit (Alt+click): the edge loop through an edge, the strip of faces across the edge
    /// nearest the mouse of a face; a point has none. Added to the selection when <paramref name="additive"/>.
    /// </summary>
    public void SelectLoop(MeshHit hit, bool additive)
    {
        if (hit.Element == MeshElement.Point)
        {
            return;
        }

        if (!additive)
        {
            ClearPrimary();
        }

        if (hit.Element == MeshElement.Edge)
        {
            IReadOnlyList<SolidEdge> loop = _topology.GetEdgeLoop(hit.Edge);
            _edges.UnionWith(loop);
        }
        else
        {
            IReadOnlyList<int> loop = _topology.GetFaceLoop(hit.Edge);
            _faces.UnionWith(loop);
        }

        Flush();
    }

    public void SelectAll()
    {
        ClearPrimary();
        switch (Element)
        {
            case MeshElement.Point:
                _points.UnionWith(Enumerable.Range(0, _solid.Points.Count));
                break;
            case MeshElement.Edge:
                _edges.UnionWith(_topology.Edges);
                break;
            default:
                _faces.UnionWith(Enumerable.Range(0, _solid.Faces.Count));
                break;
        }

        Flush();
    }

    public void Clear()
    {
        ClearPrimary();
        Flush();
    }

    private static void Toggle<T>(HashSet<T> set, T item, bool additive)
    {
        if (additive && set.Remove(item))
        {
            return;
        }

        set.Add(item);
    }

    /// <summary>Empties what the current kind holds; the other kinds follow in <see cref="Flush"/>.</summary>
    private void ClearPrimary()
    {
        _points.Clear();
        _edges.Clear();
        _faces.Clear();
    }

    /// <summary>Brings the other two kinds in line with the one the mode selects, which the mode's own kind decides.</summary>
    private void Flush()
    {
        switch (Element)
        {
            case MeshElement.Point:
                FlushFromPoints();
                break;
            case MeshElement.Edge:
                FlushFromEdges();
                break;
            default:
                FlushFromFaces();
                break;
        }
    }

    private void FlushFromPoints()
    {
        _edges.Clear();
        foreach (SolidEdge edge in _topology.Edges)
        {
            if (_points.Contains(edge.A) && _points.Contains(edge.B))
            {
                _edges.Add(edge);
            }
        }

        _faces.Clear();
        for (int face = 0; face < _solid.Faces.Count; face++)
        {
            if (_solid.Faces[face].All(_points.Contains))
            {
                _faces.Add(face);
            }
        }
    }

    private void FlushFromEdges()
    {
        _points.Clear();
        foreach (SolidEdge edge in _edges)
        {
            _points.Add(edge.A);
            _points.Add(edge.B);
        }

        _faces.Clear();
        for (int face = 0; face < _solid.Faces.Count; face++)
        {
            if (_topology.GetFaceEdges(face).All(_edges.Contains))
            {
                _faces.Add(face);
            }
        }
    }

    private void FlushFromFaces()
    {
        _edges.Clear();
        _points.Clear();
        foreach (int face in _faces)
        {
            _edges.UnionWith(_topology.GetFaceEdges(face));
            _points.UnionWith(_solid.Faces[face]);
        }
    }
}
