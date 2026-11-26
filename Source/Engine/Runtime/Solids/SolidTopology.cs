namespace FlickerVoid.Engine.Runtime.Solids;

/// <summary>
/// How a solid's points, edges and faces meet: the faces at each edge, the edges round each point in order, and the loops and rings
/// that run through quads as Blender's do. A loop follows an edge straight on through every point with four edges and ends at any
/// other; a ring crosses quads, to the edge opposite, and ends at a face that is not a quad. Works on the solid it was made from.
/// </summary>
public sealed class SolidTopology
{
    private readonly SolidMesh _solid;
    private readonly Dictionary<SolidEdge, (int Along, int Back)> _faces = [];
    private readonly List<SolidEdge> _edges = [];
    private readonly List<int>[] _pointFaces;

    public SolidTopology(SolidMesh solid)
    {
        _solid = solid;
        _pointFaces = new List<int>[solid.Points.Count];
        for (int point = 0; point < _pointFaces.Length; point++)
        {
            _pointFaces[point] = [];
        }

        for (int face = 0; face < solid.Faces.Count; face++)
        {
            IReadOnlyList<int> corners = solid.Faces[face];
            for (int corner = 0; corner < corners.Count; corner++)
            {
                int from = corners[corner];
                int to = corners[(corner + 1) % corners.Count];
                _pointFaces[from].Add(face);
                SolidEdge edge = SolidEdge.Of(from, to);
                (int along, int back) = _faces.GetValueOrDefault(edge, (-1, -1));
                if (from == edge.A)
                {
                    along = face;
                }
                else
                {
                    back = face;
                }

                if (!_faces.ContainsKey(edge))
                {
                    _edges.Add(edge);
                }

                _faces[edge] = (along, back);
            }
        }

        _edges.Sort((first, second) => first.A != second.A ? first.A.CompareTo(second.A) : first.B.CompareTo(second.B));
    }

    public IReadOnlyList<SolidEdge> Edges => _edges;

    /// <summary>The face that runs from the edge's first point to its second, and the face that runs back.</summary>
    public (int Along, int Back) GetFaces(SolidEdge edge) => _faces[edge];

    public IReadOnlyList<SolidEdge> GetFaceEdges(int face)
    {
        IReadOnlyList<int> corners = _solid.Faces[face];
        SolidEdge[] edges = new SolidEdge[corners.Count];
        for (int corner = 0; corner < edges.Length; corner++)
        {
            edges[corner] = SolidEdge.Of(corners[corner], corners[(corner + 1) % corners.Count]);
        }

        return edges;
    }

    /// <summary>
    /// The edges that meet at the point, going round it by the faces between them; empty for a point where the faces do not close
    /// into one fan, which has no loop through it.
    /// </summary>
    public IReadOnlyList<SolidEdge> GetPointEdges(int point)
    {
        List<int> around = GetNeighbours(point);
        List<SolidEdge> edges = new(around.Count);
        foreach (int neighbour in around)
        {
            edges.Add(SolidEdge.Of(point, neighbour));
        }

        return edges;
    }

    /// <summary>
    /// The edges of the loop through the edge, from the end beyond its first point to the end beyond its second, or all round when it
    /// closes. It goes straight on at a point with four edges, to the edge across from the one it came by, and ends at any other.
    /// </summary>
    public IReadOnlyList<SolidEdge> GetEdgeLoop(SolidEdge edge)
    {
        List<SolidEdge> backward = [];
        bool closed = Walk(edge, edge.B, edge.A, backward);
        if (closed)
        {
            backward.Reverse();
            return [.. backward, edge];
        }

        List<SolidEdge> forward = [];
        Walk(edge, edge.A, edge.B, forward);
        backward.Reverse();
        return [.. backward, edge, .. forward];
    }

    /// <summary>The edges parallel to this one across quads, in order from the face that runs back to the face that runs along, or all round when they close.</summary>
    public IReadOnlyList<SolidEdge> GetEdgeRing(SolidEdge edge)
    {
        (List<SolidEdge> edges, _) = GetRing(edge);
        return edges;
    }

    /// <summary>The quads between the edges of the ring through this edge, in the ring's order.</summary>
    public IReadOnlyList<int> GetFaceLoop(SolidEdge edge)
    {
        (_, List<int> faces) = GetRing(edge);
        return faces;
    }

    /// <summary>Follows the loop from the edge, coming from <paramref name="away"/> and on past <paramref name="pivot"/>; true when it comes back to the edge it began on.</summary>
    private bool Walk(SolidEdge first, int away, int pivot, List<SolidEdge> found)
    {
        int from = away;
        int at = pivot;
        while (true)
        {
            List<int> around = GetNeighbours(at);
            if (around.Count != 4)
            {
                return false;
            }

            int index = around.IndexOf(from);
            int across = around[(index + 2) % 4];
            SolidEdge next = SolidEdge.Of(at, across);
            if (next == first)
            {
                return true;
            }

            found.Add(next);
            from = at;
            at = across;
        }
    }

    private (List<SolidEdge> Edges, List<int> Faces) GetRing(SolidEdge edge)
    {
        (int along, int back) = _faces[edge];
        List<SolidEdge> backEdges = [];
        List<int> backFaces = [];
        bool closed = Cross(edge, back, backEdges, backFaces);
        List<SolidEdge> alongEdges = [];
        List<int> alongFaces = [];
        if (!closed)
        {
            Cross(edge, along, alongEdges, alongFaces);
        }

        backEdges.Reverse();
        backFaces.Reverse();
        return ([.. backEdges, edge, .. alongEdges], [.. backFaces, .. alongFaces]);
    }

    /// <summary>Crosses quads away from the edge, starting into <paramref name="face"/>; true when the ring closes back on the edge.</summary>
    private bool Cross(SolidEdge first, int face, List<SolidEdge> edges, List<int> faces)
    {
        SolidEdge current = first;
        int into = face;
        while (_solid.Faces[into].Count == 4)
        {
            SolidEdge opposite = GetOpposite(into, current);
            faces.Add(into);
            if (opposite == first)
            {
                return true;
            }

            edges.Add(opposite);
            (int along, int back) = _faces[opposite];
            if (along == into)
            {
                into = back;
            }
            else
            {
                into = along;
            }

            current = opposite;
        }

        return false;
    }

    private SolidEdge GetOpposite(int quad, SolidEdge edge)
    {
        IReadOnlyList<SolidEdge> edges = GetFaceEdges(quad);
        int index = edges.ToList().IndexOf(edge);
        return edges[(index + 2) % 4];
    }

    /// <summary>The points joined to this one, going round it; empty unless the faces at the point close into one fan.</summary>
    private List<int> GetNeighbours(int point)
    {
        List<int> neighbours = [];
        List<int> faces = _pointFaces[point];
        if (faces.Count == 0)
        {
            return neighbours;
        }

        int face = faces[0];
        int start = GetNext(face, point);
        int to = start;
        do
        {
            neighbours.Add(to);
            // The face that runs from this neighbour into the point has, after the point, the next neighbour round it.
            int turn = GetFaceRunning(point, to);
            to = GetNext(turn, point);
            if (neighbours.Count > faces.Count)
            {
                return [];
            }
        }
        while (to != start);

        if (neighbours.Count != faces.Count)
        {
            return [];
        }

        return neighbours;
    }

    /// <summary>The face with an edge running from <paramref name="from"/> into <paramref name="point"/>.</summary>
    private int GetFaceRunning(int point, int from)
    {
        SolidEdge edge = SolidEdge.Of(from, point);
        (int along, int back) = _faces[edge];
        if (edge.A == from)
        {
            return along;
        }

        return back;
    }

    /// <summary>The point after <paramref name="point"/> in the face.</summary>
    private int GetNext(int face, int point)
    {
        IReadOnlyList<int> corners = _solid.Faces[face];
        int index = -1;
        for (int corner = 0; corner < corners.Count; corner++)
        {
            if (corners[corner] == point)
            {
                index = corner;
            }
        }

        return corners[(index + 1) % corners.Count];
    }
}
