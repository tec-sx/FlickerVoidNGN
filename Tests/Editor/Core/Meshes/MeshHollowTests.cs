using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshHollowTests
{
    private readonly SolidMesh _box = SolidMesh.Box(new Vector3(2, 2, 2));

    [Fact]
    public void TheInnerCopyOfABoxIsSmallerByTheWallOnEverySide()
    {
        SolidMesh inner = MeshHollow.Offset(_box, 0.1f, new HashSet<int>());

        inner.Points.Min(point => point.X).Should().BeApproximately(-0.9f, 1e-3f);
        inner.Points.Max(point => point.X).Should().BeApproximately(0.9f, 1e-3f);
        inner.Points.Min(point => point.Y).Should().BeApproximately(0.1f, 1e-3f);
        inner.Points.Max(point => point.Y).Should().BeApproximately(1.9f, 1e-3f);
    }

    [Fact]
    public void AnOpenFaceIsBrokenThroughByTheInnerCopy()
    {
        int top = FindFace(Vector3.UnitY);

        SolidMesh inner = MeshHollow.Offset(_box, 0.1f, new HashSet<int> { top });

        inner.Points.Max(point => point.Y).Should().BeGreaterThan(2, "the inner copy pokes out of the open top");
        inner.Points.Min(point => point.Y).Should().BeApproximately(0.1f, 1e-3f);
    }

    [Fact]
    public void AHollowBoxIsAClosedShellWithMoreFaces()
    {
        Assert.SkipUnless(FlickerVoid.Editor.Core.Meshes.Booleans.SolidBoolean.IsAvailable, "Manifold's library is not built.");

        MeshResult? shell = MeshHollow.Shell(_box, 0.1f, new HashSet<int>());

        shell.Should().NotBeNull();
        shell!.Solid.Faces.Count.Should().Be(12, "the outer and the inner box");
    }

    [Fact]
    public void AnOpenTopMakesATray()
    {
        Assert.SkipUnless(FlickerVoid.Editor.Core.Meshes.Booleans.SolidBoolean.IsAvailable, "Manifold's library is not built.");
        int top = FindFace(Vector3.UnitY);

        MeshResult? tray = MeshHollow.Shell(_box, 0.1f, new HashSet<int> { top });

        tray.Should().NotBeNull();
        tray!.Solid.Points.Should().Contain(point => MathF.Abs(point.Y - 2) < 1e-3f && MathF.Abs(point.X) < 0.95f && MathF.Abs(point.X) > 0.85f, "the rim's inner edge");
    }

    [Fact]
    public void NoWallOrAWallThickerThanTheBoxGivesNothing()
    {
        Assert.SkipUnless(FlickerVoid.Editor.Core.Meshes.Booleans.SolidBoolean.IsAvailable, "Manifold's library is not built.");

        MeshHollow.Shell(_box, 0, new HashSet<int>()).Should().BeNull();
        MeshHollow.Shell(_box, 1.5f, new HashSet<int>()).Should().BeNull();
    }

    private int FindFace(Vector3 normal)
    {
        for (int face = 0; face < _box.Faces.Count; face++)
        {
            IReadOnlyList<int> corners = _box.Faces[face];
            Vector3 sum = Vector3.Zero;
            for (int corner = 0; corner < corners.Count; corner++)
            {
                sum += Vector3.Cross(_box.Points[corners[corner]], _box.Points[corners[(corner + 1) % corners.Count]]);
            }

            if (Vector3.Dot(Vector3.Normalize(sum), normal) > 0.99f)
            {
                return face;
            }
        }

        return -1;
    }
}
