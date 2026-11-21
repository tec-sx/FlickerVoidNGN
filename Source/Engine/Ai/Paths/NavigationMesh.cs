using System.Numerics;

using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using DotRecast.Recast.Geom;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>
/// The ground characters can walk on, built by Recast from triangles (a single tile), and the paths across it found by Detour: the
/// corners of the shortest way from one point to another, or to the nearest point that can be reached.
/// </summary>
public sealed class NavigationMesh
{
    private const int MaxPolygons = 256;
    private const int VerticesPerPolygon = 6;
    private const int RegionMinSize = 8;
    private const int RegionMergeSize = 20;
    private const float EdgeMaxLength = 12;
    private const float EdgeMaxError = 1.3f;
    private const float DetailSampleDistance = 6;
    private const float DetailSampleMaxError = 1;
    private const int WalkableFlag = 1;

    private readonly DtNavMeshQuery _query;
    private readonly DtQueryDefaultFilter _filter = new();
    private readonly RcVec3f _searchExtents;
    private readonly long[] _polygons = new long[MaxPolygons];
    private readonly DtStraightPath[] _straight = new DtStraightPath[MaxPolygons];

    private NavigationMesh(DtNavMesh mesh, NavigationSettings settings)
    {
        _query = new DtNavMeshQuery(mesh);
        _searchExtents = new RcVec3f(settings.SearchDistance, settings.SearchHeight, settings.SearchDistance);
    }

    /// <summary>Builds the walkable ground of the triangles (three vertex indices each); null when nothing can be walked on.</summary>
    public static NavigationMesh? Build(float[] vertices, int[] triangles, NavigationSettings settings)
    {
        if (triangles.Length == 0)
        {
            return null;
        }

        RcSampleInputGeomProvider geometry = new(vertices, triangles);
        RcAreaModification walkable = new(RcRecast.RC_WALKABLE_AREA);
        RcConfig config = new(
            RcPartition.WATERSHED,
            settings.CellSize,
            settings.CellHeight,
            settings.MaxSlope,
            settings.AgentHeight,
            settings.AgentRadius,
            settings.MaxClimb,
            RegionMinSize,
            RegionMergeSize,
            EdgeMaxLength,
            EdgeMaxError,
            VerticesPerPolygon,
            DetailSampleDistance,
            DetailSampleMaxError,
            true,
            true,
            true,
            walkable,
            true);
        RcVec3f min = geometry.GetMeshBoundsMin();
        RcVec3f max = geometry.GetMeshBoundsMax();
        RcBuilderConfig builderConfig = new(config, min, max);
        RcBuilder builder = new();
        RcBuilderResult result = builder.Build(geometry, builderConfig, false);
        RcPolyMesh polygons = result.Mesh;
        if (polygons.npolys == 0)
        {
            return null;
        }

        for (int index = 0; index < polygons.npolys; index++)
        {
            if (polygons.areas[index] != RcRecast.RC_NULL_AREA)
            {
                polygons.flags[index] = WalkableFlag;
            }
        }

        DtNavMeshCreateParams parameters = CreateParameters(config, polygons, result.MeshDetail, settings);
        DtDetourBuilder detour = new();
        DtMeshData data = detour.Build(parameters, 0, 0);
        DtNavMesh mesh = new();
        mesh.Init(data, VerticesPerPolygon, 0);
        return new NavigationMesh(mesh, settings);
    }

    /// <summary>
    /// Fills <paramref name="corners"/> with the points from <paramref name="from"/> (not included) to <paramref name="to"/>, or to the
    /// nearest point of the ground that can be reached; false when either point has no ground near it.
    /// </summary>
    public bool TryFindPath(Vector3 from, Vector3 to, List<Vector3> corners)
    {
        RcVec3f start = ToRecast(from);
        RcVec3f end = ToRecast(to);
        DtStatus startFound = _query.FindNearestPoly(start, _searchExtents, _filter, out long startPolygon, out RcVec3f startPoint, out _);
        DtStatus endFound = _query.FindNearestPoly(end, _searchExtents, _filter, out long endPolygon, out RcVec3f endPoint, out _);
        if (startFound.Failed() || endFound.Failed() || startPolygon == 0 || endPolygon == 0)
        {
            return false;
        }

        Span<long> polygons = _polygons;
        DtStatus pathFound = _query.FindPath(startPolygon, endPolygon, startPoint, endPoint, _filter, polygons, out int polygonCount, MaxPolygons);
        if (pathFound.Failed() || polygonCount == 0)
        {
            return false;
        }

        long last = polygons[polygonCount - 1];
        if (last != endPolygon)
        {
            _query.ClosestPointOnPoly(last, endPoint, out endPoint, out _);
        }

        Span<DtStraightPath> straight = _straight;
        _query.FindStraightPath(startPoint, endPoint, polygons, polygonCount, straight, out int cornerCount, MaxPolygons, 0);
        for (int index = 1; index < cornerCount; index++)
        {
            RcVec3f corner = straight[index].pos;
            corners.Add(new Vector3(corner.X, corner.Y, corner.Z));
        }

        return corners.Count > 0;
    }

    private static DtNavMeshCreateParams CreateParameters(RcConfig config, RcPolyMesh polygons, RcPolyMeshDetail detail, NavigationSettings settings)
    {
        return new DtNavMeshCreateParams
        {
            verts = polygons.verts,
            vertCount = polygons.nverts,
            polys = polygons.polys,
            polyAreas = polygons.areas,
            polyFlags = polygons.flags,
            polyCount = polygons.npolys,
            nvp = polygons.nvp,
            detailMeshes = detail.meshes,
            detailVerts = detail.verts,
            detailVertsCount = detail.nverts,
            detailTris = detail.tris,
            detailTriCount = detail.ntris,
            walkableHeight = settings.AgentHeight,
            walkableRadius = settings.AgentRadius,
            walkableClimb = settings.MaxClimb,
            bmin = polygons.bmin,
            bmax = polygons.bmax,
            cs = config.Cs,
            ch = config.Ch,
            buildBvTree = true,
        };
    }

    private static RcVec3f ToRecast(Vector3 point) => new(point.X, point.Y, point.Z);
}
