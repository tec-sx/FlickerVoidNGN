using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshCutTests
{
    private const int Top = 2;

    private readonly SolidMesh _slab = SolidMesh.Box(new Vector3(2, 1, 2));

    [Fact]
    public void APocketPushesTheFaceIntoTheSolid()
    {
        MeshResult? result = MeshCut.Faces(_slab, new HashSet<int> { Top }, 0.4f, through: false);

        result.Should().NotBeNull();
        result!.Solid.Points.Min(point => point.Y).Should().BeApproximately(0, 1e-5f);
        IReadOnlyList<int> floor = result.Solid.Faces[result.Faces[0]];
        floor.Select(point => result.Solid.Points[point].Y).Should().AllSatisfy(y => y.Should().BeApproximately(0.6f, 1e-5f));
    }

    [Fact]
    public void NoFacesOrNoDepthCutsNothing()
    {
        MeshCut.Faces(_slab, new HashSet<int>(), 0.4f, through: false).Should().BeNull();
        MeshCut.Faces(_slab, new HashSet<int> { Top }, 0, through: false).Should().BeNull();
    }

    [Fact]
    public void AnInsetFaceCutThroughMakesAnOpeningThatTakesItsVolume()
    {
        SolidBooleanTestsBuilder.RequireLibrary();
        MeshResult? inset = MeshInset.Faces(_slab, new HashSet<int> { Top }, 0.5f);
        int inner = inset!.Faces[0];

        MeshResult? result = MeshCut.Faces(inset.Solid, new HashSet<int> { inner }, 0, through: true);

        result.Should().NotBeNull();
        SolidBooleanTestsBuilder.GetVolume(result!.Solid).Should().BeApproximately((2 * 2 * 1) - (1 * 1 * 1), 1e-3f);
    }
}
