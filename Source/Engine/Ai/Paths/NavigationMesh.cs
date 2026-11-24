using System.Numerics;

using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using DotRecast.Recast.Geom;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>
/// The ground characters can walk on, built by Recast from triangles in square tiles, and the paths across it found by Detour: the
/// corners of the shortest way from one point to another, or to the nearest point that can be reached. When obstacles change, only the
/// tiles they touch are built again.
/// </summary>
public sealed class NavigationMesh
{
    private const int MaxPolygons = 1024;
    private const int VerticesPerPolygon = 6;
    private const int RegionMinSize = 8;
    private const int RegionMergeSize = 20;
    private const float EdgeMaxLength = 12;
    private const float EdgeMaxError = 1.3f;
    private const float DetailSampleDistance = 6;
    private const float DetailSampleMaxError = 1;
    private const int WalkableFlag = 1;
    private const int MaxTileBits = 14;
    private const int PolygonAndTileBits = 22;

    private readonly DtNavMesh _mesh;
    private readonly DtNavMeshQuery _query;
    private readonly DtQueryDefaultFilter _filter = new();
    private readonly RcConfig _config;
    private readonly NavigationSettings _settings;
    private readonly RcVec3f _min;
    private readonly RcVec3f _max;
    private readonly int _tilesX;
    private readonly int _tilesZ;
    private readonly RcVec3f _searchExtents;
    private readonly long[] _polygons = new long[MaxPolygons];
    private readonly DtStraightPath[] _straight = new DtStraightPath[MaxPolygons];

    private NavigationMesh(DtNavMesh mesh, RcConfig config, NavigationSettings settings, RcVec3f min, RcVec3f max, int tilesX, int tilesZ)
    {
        _mesh = mesh;
        _query = new DtNavMeshQuery(mesh);
        _config = config;
        _settings = settings;
        _min = min;
        _max = max;
        _tilesX = tilesX;
        _tilesZ = tilesZ;
        _searchExtents = new RcVec3f(settings.SearchDistance, settings.SearchHeight, settings.SearchDistance);
    }

    /// <summary>How many tiles hold walkable ground.</summary>
    public int TileCount { get; private set; }

    /// <summary>Builds the walkable ground of the triangles (three vertex indices each); null when nothing can be walked on.</summary>
    public static NavigationMesh? Build(List<float> vertices, List<int> triangles, NavigationSettings settings)
    {
        if (triangles.Count == 0)
        {
            return null;
        }

        RcSampleInputGeomProvider geometry = new(vertices, triangles);
        RcConfig config = CreateConfig(settings);
        RcVec3f min = geometry.GetMeshBoundsMin();
        RcVec3f max = geometry.GetMeshBoundsMax();
        RcRecast.CalcTileCount(min, max, config.Cs, config.TileSizeX, config.TileSizeZ, out int tilesX, out int tilesZ);
        int tileBits = Math.Min(DtUtils.Ilog2(DtUtils.NextPow2(tilesX * tilesZ)), MaxTileBits);
        float tileWidth = config.TileSizeX * config.Cs;
        DtNavMeshParams parameters = new()
        {
            orig = min,
            tileWidth = tileWidth,
            tileHeight = tileWidth,
            maxTiles = 1 << tileBits,
            maxPolys = 1 << (PolygonAndTileBits - tileBits),
        };
        DtNavMesh mesh = new();
        mesh.Init(parameters, VerticesPerPolygon);
        NavigationMesh navigation = new(mesh, config, settings, min, max, tilesX, tilesZ);
        for (int z = 0; z < tilesZ; z++)
        {
            for (int x = 0; x < tilesX; x++)
            {
                navigation.BuildTile(geometry, x, z);
            }
        }

        if (navigation.TileCount == 0)
        {
            return null;
        }

        return navigation;
    }

    /// <summary>
    /// Builds again, from the triangles as they are now, the tiles within reach of the areas that changed (what an obstacle covered
    /// before and covers now); false, building nothing, when the triangles reach past the mesh's bounds, so it must be built anew.
    /// </summary>
    public bool Rebuild(List<float> vertices, List<int> triangles, IReadOnlyList<Bounds> changed)
    {
        if (triangles.Count == 0)
        {
            return false;
        }

        RcSampleInputGeomProvider geometry = new(vertices, triangles);
        RcVec3f min = geometry.GetMeshBoundsMin();
        RcVec3f max = geometry.GetMeshBoundsMax();
        bool inside = min.X >= _min.X && min.Y >= _min.Y && min.Z >= _min.Z && max.X <= _max.X && max.Y <= _max.Y && max.Z <= _max.Z;
        if (!inside)
        {
            return false;
        }

        float tileWidth = _config.TileSizeX * _config.Cs;
        float reach = _config.BorderSize * _config.Cs;
        HashSet<(int X, int Z)> tiles = [];
        foreach (Bounds area in changed)
        {
            int firstX = Math.Clamp((int)MathF.Floor((area.Min.X - reach - _min.X) / tileWidth), 0, _tilesX - 1);
            int lastX = Math.Clamp((int)MathF.Floor((area.Max.X + reach - _min.X) / tileWidth), 0, _tilesX - 1);
            int firstZ = Math.Clamp((int)MathF.Floor((area.Min.Z - reach - _min.Z) / tileWidth), 0, _tilesZ - 1);
            int lastZ = Math.Clamp((int)MathF.Floor((area.Max.Z + reach - _min.Z) / tileWidth), 0, _tilesZ - 1);
            for (int z = firstZ; z <= lastZ; z++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    tiles.Add((x, z));
                }
            }
        }

        foreach ((int x, int z) in tiles)
        {
            RemoveTile(x, z);
            BuildTile(geometry, x, z);
        }

        return true;
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

    /// <summary>
    /// Where a step from <paramref name="from"/> toward <paramref name="to"/> ends sliding along the walkable ground's edges, at
    /// <paramref name="to"/>'s height; <paramref name="to"/> itself when <paramref name="from"/> has no ground near it.
    /// </summary>
    public Vector3 Constrain(Vector3 from, Vector3 to)
    {
        RcVec3f start = ToRecast(from);
        DtStatus found = _query.FindNearestPoly(start, _searchExtents, _filter, out long polygon, out RcVec3f startPoint, out _);
        if (found.Failed() || polygon == 0)
        {
            return to;
        }

        RcVec3f end = ToRecast(to);
        Span<long> visited = _polygons;
        DtStatus moved = _query.MoveAlongSurface(polygon, startPoint, end, _filter, out RcVec3f reached, visited, out _, MaxPolygons);
        if (moved.Failed())
        {
            return to;
        }

        return new Vector3(reached.X, to.Y, reached.Z);
    }

    /// <summary>A random point of the ground within the radius of a point, reachable from the ground nearest to it; false when there is none.</summary>
    public bool TryFindRandomPoint(Vector3 around, float radius, Random random, out Vector3 point)
    {
        point = around;
        RcVec3f center = ToRecast(around);
        DtStatus found = _query.FindNearestPoly(center, _searchExtents, _filter, out long polygon, out RcVec3f nearest, out _);
        if (found.Failed() || polygon == 0)
        {
            return false;
        }

        RandomSource source = new(random);
        DtStatus picked = _query.FindRandomPointWithinCircle(polygon, nearest, radius, _filter, source, out _, out RcVec3f randomPoint);
        if (picked.Failed())
        {
            return false;
        }

        point = new Vector3(randomPoint.X, randomPoint.Y, randomPoint.Z);
        return true;
    }

    private static RcConfig CreateConfig(NavigationSettings settings)
    {
        float cellArea = settings.CellSize * settings.CellSize;
        RcAreaModification walkable = new(RcRecast.RC_WALKABLE_AREA);
        return new RcConfig(
            true,
            settings.TileSize,
            settings.TileSize,
            RcConfig.CalcBorder(settings.AgentRadius, settings.CellSize),
            RcPartition.WATERSHED,
            settings.CellSize,
            settings.CellHeight,
            settings.MaxSlope,
            settings.AgentHeight,
            settings.AgentRadius,
            settings.MaxClimb,
            RegionMinSize * RegionMinSize * cellArea,
            RegionMergeSize * RegionMergeSize * cellArea,
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
    }

    private void RemoveTile(int x, int z)
    {
        long tile = _mesh.GetTileRefAt(x, z, 0);
        if (tile == 0)
        {
            return;
        }

        _mesh.RemoveTile(tile);
        TileCount--;
    }

    private void BuildTile(RcSampleInputGeomProvider geometry, int x, int z)
    {
        RcBuilderConfig builderConfig = new(_config, _min, _max, x, z);
        RcBuilder builder = new();
        RcBuilderResult result = builder.Build(geometry, builderConfig, false);
        RcPolyMesh polygons = result.Mesh;
        if (polygons.npolys == 0)
        {
            return;
        }

        for (int index = 0; index < polygons.npolys; index++)
        {
            if (polygons.areas[index] != RcRecast.RC_NULL_AREA)
            {
                polygons.flags[index] = WalkableFlag;
            }
        }

        DtNavMeshCreateParams parameters = CreateParameters(polygons, result.MeshDetail, x, z);
        DtDetourBuilder detour = new();
        DtMeshData? data = detour.Build(parameters, x, z);
        if (data is null)
        {
            return;
        }

        _mesh.AddTile(data, 0, 0, out _);
        TileCount++;
    }

    private DtNavMeshCreateParams CreateParameters(RcPolyMesh polygons, RcPolyMeshDetail detail, int x, int z)
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
            walkableHeight = _settings.AgentHeight,
            walkableRadius = _settings.AgentRadius,
            walkableClimb = _settings.MaxClimb,
            bmin = polygons.bmin,
            bmax = polygons.bmax,
            cs = _config.Cs,
            ch = _config.Ch,
            buildBvTree = true,
            tileX = x,
            tileZ = z,
        };
    }

    private static RcVec3f ToRecast(Vector3 point) => new(point.X, point.Y, point.Z);
}
