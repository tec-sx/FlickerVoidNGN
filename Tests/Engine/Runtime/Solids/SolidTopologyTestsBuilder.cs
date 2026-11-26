using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Runtime.Tests.Solids;

/// <summary>Builds the topology under test over a solid: a box, a bar of square rings or a prism.</summary>
internal sealed class SolidTopologyTestsBuilder
{
    private SolidMesh _solid = SolidMesh.Box(Vector3.One);

    public SolidTopologyTestsBuilder WithBox()
    {
        _solid = SolidMesh.Box(Vector3.One);
        return this;
    }

    /// <summary>A bar along the up axis of <paramref name="rings"/> rings of <paramref name="sides"/> points joined by quads, closed by a cap at each end.</summary>
    public SolidTopologyTestsBuilder WithBar(int rings, int sides = 4)
    {
        Vector3[] points = new Vector3[rings * sides];
        for (int ring = 0; ring < rings; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                float angle = MathF.Tau * side / sides;
                points[GetPoint(ring, side, sides)] = new Vector3(MathF.Cos(angle), ring, -MathF.Sin(angle));
            }
        }

        List<int[]> faces = [];
        for (int ring = 0; ring + 1 < rings; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int[] face = [GetPoint(ring, side, sides), GetPoint(ring, next, sides), GetPoint(ring + 1, next, sides), GetPoint(ring + 1, side, sides)];
                faces.Add(face);
            }
        }

        faces.Add([.. Enumerable.Range(0, sides).Select(side => GetPoint(rings - 1, side, sides))]);
        faces.Add([.. Enumerable.Range(0, sides).Reverse().Select(side => GetPoint(0, side, sides))]);
        _solid = new SolidMesh(points, faces.Select(face => (IReadOnlyList<int>)face).ToList());
        return this;
    }

    public SolidTopologyTestsBuilder WithPrism(int sides)
    {
        _solid = SolidMesh.Cylinder(1, 1, sides);
        return this;
    }

    public SolidMesh Solid => _solid;

    public SolidTopology Build() => new(_solid);

    public static int GetPoint(int ring, int side, int sides = 4) => (ring * sides) + side;
}
