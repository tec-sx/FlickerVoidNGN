using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshEditTests
{
    private readonly SolidMesh _box = MeshTestsBuilder.Box();

    [Fact]
    public void MovesOnlyTheGivenPointsAndKeepsTheFaces()
    {
        HashSet<int> top = [2, 3, 6, 7];

        SolidMesh moved = MeshEdit.Move(_box, top, new Vector3(0, 1, 0));

        moved.Points[2].Y.Should().BeApproximately(2, 1e-5f);
        moved.Points[0].Should().Be(_box.Points[0]);
        moved.Faces.Should().BeEquivalentTo(_box.Faces);
    }

    [Fact]
    public void RotatesAboutTheMiddleOfThePoints()
    {
        HashSet<int> all = [.. Enumerable.Range(0, 8)];
        Vector3 centre = MeshEdit.GetCentre(_box, all);

        SolidMesh turned = MeshEdit.Rotate(_box, all, new Vector3(0, 90, 0));

        Vector3.Distance(MeshEdit.GetCentre(turned, all), centre).Should().BeLessThan(1e-5f);
        turned.Points[0].Should().NotBe(_box.Points[0]);
    }

    [Fact]
    public void ScalesAboutTheMiddleOfThePoints()
    {
        HashSet<int> top = [2, 3, 6, 7];

        SolidMesh scaled = MeshEdit.Scale(_box, top, new Vector3(2, 1, 1));

        scaled.Points[2].X.Should().BeApproximately(-1, 1e-5f);
        scaled.Points[3].X.Should().BeApproximately(1, 1e-5f);
        scaled.Points[2].Y.Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void NoPointsChangeNothing()
    {
        SolidMesh moved = MeshEdit.Move(_box, new HashSet<int>(), Vector3.One);

        moved.Points.Should().Equal(_box.Points);
    }
}
