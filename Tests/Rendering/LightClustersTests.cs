using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class LightClustersTests
{
    private const float Near = LightClustersTestsBuilder.Near;
    private const float Far = LightClustersTestsBuilder.Far;

    private readonly LightClustersTestsBuilder _builder = new();

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(LightClusters.Slices - 1)]
    public void SplitsAnOrthographicViewIntoEvenSlices(int slice)
    {
        LightClusters clusters = _builder.Build();
        float sliceDepth = (Far - Near) / LightClusters.Slices;
        float start = Near + (sliceDepth * slice);

        int found = clusters.GetSlice(start + (sliceDepth * 0.01f));

        found.Should().Be(slice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(LightClusters.Slices - 1)]
    public void SplitsAPerspectiveViewIntoSlicesThatDeepenWithDistance(int slice)
    {
        LightClusters clusters = _builder.WithPerspective(60).Build();
        float start = Near * MathF.Pow(Far / Near, (float)slice / LightClusters.Slices);

        int found = clusters.GetSlice(start * 1.01f);

        found.Should().Be(slice);
    }

    [Fact]
    public void ClampsDepthsOutsideTheViewToTheFirstAndLastSlice()
    {
        LightClusters clusters = _builder.Build();

        clusters.GetSlice(0).Should().Be(0);
        clusters.GetSlice(Far * 2).Should().Be(LightClusters.Slices - 1);
    }

    [Fact]
    public void PutsALightInTheClustersItReaches()
    {
        LightClusters clusters = _builder.WithLight(Vector3.Zero, 0.4f).Build();
        int slice = clusters.GetSlice(LightClustersTestsBuilder.CameraZ);

        clusters.GetLights(7, 4, slice).ToArray().Should().Equal(0u);
        clusters.GetLights(8, 4, slice).ToArray().Should().Equal(0u);
        clusters.Indices.Length.Should().Be(2, "the light reaches only the two tiles at the centre of the view, in one slice");
    }

    [Fact]
    public void ListsEveryLightOfACluster()
    {
        LightClusters clusters = _builder.WithLight(new Vector3(0.5f, 0, 0), 0.1f).WithLight(new Vector3(0.6f, 0, 0), 0.1f).Build();
        int slice = clusters.GetSlice(LightClustersTestsBuilder.CameraZ);

        clusters.GetLights(8, 4, slice).ToArray().Should().Equal(0u, 1u);
    }

    [Fact]
    public void LeavesOutLightsBehindTheCamera()
    {
        LightClusters clusters = _builder.WithLight(new Vector3(0, 0, 10), 1).Build();

        clusters.Indices.Length.Should().Be(0);
    }

    [Fact]
    public void PutsALightThatFillsTheViewInEveryCluster()
    {
        LightClusters clusters = _builder.WithLight(new Vector3(0, 0, -45), 1000).Build();

        clusters.Indices.Length.Should().Be(LightClusters.Count);
    }

    [Fact]
    public void WidensPerspectiveTilesWithDepth()
    {
        const float depth = 20;
        float halfWidth = depth * 16 / 9;
        Vector3 nearTheRightEdge = new(halfWidth * 0.95f, 0, LightClustersTestsBuilder.CameraZ - depth);
        LightClusters clusters = _builder.WithPerspective(90).WithLight(nearTheRightEdge, 0.1f).Build();
        int slice = clusters.GetSlice(depth);

        clusters.GetLights(LightClusters.TilesX - 1, 4, slice).ToArray().Should().Equal(0u);
        clusters.GetLights(LightClusters.TilesX / 2, 4, slice).Length.Should().Be(0, "the light sits at the right edge of the view");
    }
}
