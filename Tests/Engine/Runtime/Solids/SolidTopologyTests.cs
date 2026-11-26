using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Runtime.Tests.Solids;

public sealed class SolidTopologyTests
{
    private readonly SolidTopologyTestsBuilder _builder = new();

    [Fact]
    public void ABoxHasTwelveEdgesWithTheSmallerPointFirst()
    {
        SolidTopology topology = _builder.WithBox().Build();

        topology.Edges.Should().HaveCount(12);
        topology.Edges.Should().OnlyContain(edge => edge.A < edge.B);
        topology.Edges.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EdgeIsTheSameWhicheverWayItIsNamed()
    {
        SolidEdge.Of(5, 2).Should().Be(SolidEdge.Of(2, 5));
        SolidEdge.Of(5, 2).Should().Be(new SolidEdge(2, 5));
    }

    [Fact]
    public void EachEdgeHasTheFaceThatRunsAlongItAndTheFaceThatRunsBack()
    {
        SolidTopology topology = _builder.WithBar(2).Build();
        SolidEdge edge = SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(0, 0), SolidTopologyTestsBuilder.GetPoint(0, 1));

        (int along, int back) = topology.GetFaces(edge);

        _builder.Solid.Faces[along].Should().ContainInOrder(edge.A, edge.B);
        _builder.Solid.Faces[back].Should().ContainInOrder(edge.B, edge.A);
        along.Should().NotBe(back);
    }

    [Fact]
    public void AFaceListsItsEdgesInOrder()
    {
        SolidTopology topology = _builder.WithBox().Build();

        IReadOnlyList<SolidEdge> edges = topology.GetFaceEdges(1);

        IReadOnlyList<int> face = _builder.Solid.Faces[1];
        edges.Should().Equal(face.Select((point, corner) => SolidEdge.Of(point, face[(corner + 1) % face.Count])));
    }

    [Fact]
    public void AVertexListsItsEdgesAroundIt()
    {
        SolidTopology topology = _builder.WithBar(3).Build();
        int middle = SolidTopologyTestsBuilder.GetPoint(1, 0);
        int corner = SolidTopologyTestsBuilder.GetPoint(0, 0);

        IReadOnlyList<SolidEdge> around = topology.GetPointEdges(middle);

        around.Should().HaveCount(4);
        around.Should().OnlyContain(edge => edge.A == middle || edge.B == middle);
        topology.GetPointEdges(corner).Should().HaveCount(3);
    }

    [Fact]
    public void EdgeLoopStopsAtAVertexThatIsNotFourWay()
    {
        SolidTopology topology = _builder.WithBox().Build();
        SolidEdge edge = topology.Edges[0];

        IReadOnlyList<SolidEdge> loop = topology.GetEdgeLoop(edge);

        loop.Should().Equal(edge);
    }

    [Fact]
    public void EdgeLoopRunsRoundARingOfFourWayVerticesAndCloses()
    {
        SolidTopology topology = _builder.WithBar(3).Build();
        SolidEdge edge = SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(1, 0), SolidTopologyTestsBuilder.GetPoint(1, 1));

        IReadOnlyList<SolidEdge> loop = topology.GetEdgeLoop(edge);

        loop.Should().HaveCount(4);
        loop.Should().Contain(edge);
        loop.Should().OnlyContain(member => member.A >= 4 && member.B < 8);
        loop.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EdgeLoopRunsAlongTheBarAndStopsAtTheCaps()
    {
        SolidTopology topology = _builder.WithBar(4).Build();
        SolidEdge edge = SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(1, 0), SolidTopologyTestsBuilder.GetPoint(2, 0));

        IReadOnlyList<SolidEdge> loop = topology.GetEdgeLoop(edge);

        IEnumerable<SolidEdge> along = Enumerable.Range(0, 3).Select(ring => SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(ring, 0), SolidTopologyTestsBuilder.GetPoint(ring + 1, 0)));
        loop.Should().Equal(along);
    }

    [Fact]
    public void EdgeLoopStartingAtTheOtherEndRunsTheSameWayRound()
    {
        SolidTopology topology = _builder.WithBar(4).Build();
        SolidEdge first = SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(0, 0), SolidTopologyTestsBuilder.GetPoint(1, 0));
        SolidEdge last = SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(2, 0), SolidTopologyTestsBuilder.GetPoint(3, 0));

        IReadOnlyList<SolidEdge> fromFirst = topology.GetEdgeLoop(first);
        IReadOnlyList<SolidEdge> fromLast = topology.GetEdgeLoop(last);

        fromLast.Should().Equal(fromFirst);
    }

    [Fact]
    public void EdgeRingCrossesQuadsAndRunsRoundTheBarThroughItsCaps()
    {
        SolidTopology topology = _builder.WithBar(4).Build();
        SolidEdge edge = SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(1, 0), SolidTopologyTestsBuilder.GetPoint(1, 1));

        IReadOnlyList<SolidEdge> ring = topology.GetEdgeRing(edge);
        IReadOnlyList<int> faces = topology.GetFaceLoop(edge);

        ring.Should().HaveCount(8);
        ring.Should().Contain(edge);
        faces.Should().HaveCount(8);
        faces.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EdgeRingStopsAtAFaceThatIsNotAQuad()
    {
        SolidTopology topology = _builder.WithPrism(3).Build();
        SolidEdge edge = SolidEdge.Of(0, 1);

        IReadOnlyList<SolidEdge> ring = topology.GetEdgeRing(edge);
        IReadOnlyList<int> faces = topology.GetFaceLoop(edge);

        ring.Should().Equal(edge, SolidEdge.Of(3, 4));
        faces.Should().Equal(0);
    }

    [Fact]
    public void FaceLoopFollowsTheStripAcrossTheEdgeTheMouseIsNear()
    {
        SolidTopology topology = _builder.WithBar(4).Build();
        SolidEdge along = SolidEdge.Of(SolidTopologyTestsBuilder.GetPoint(1, 0), SolidTopologyTestsBuilder.GetPoint(2, 0));

        IReadOnlyList<int> faces = topology.GetFaceLoop(along);

        IEnumerable<int> middleSegment = Enumerable.Range(4, 4);
        faces.Should().BeEquivalentTo(middleSegment);
        faces.Should().OnlyHaveUniqueItems();
    }
}
