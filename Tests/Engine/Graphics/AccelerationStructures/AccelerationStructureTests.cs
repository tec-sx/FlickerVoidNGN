using System.Numerics;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Resources;

namespace FlickerVoid.Engine.Graphics.Tests.AccelerationStructures;

public sealed class AccelerationStructureTests : IDisposable
{
    private const float Precision = 1e-4f;

    /// <summary>Instances enough that building a top-level structure for them takes more scratch than refitting a quad.</summary>
    private const int LargeScene = 4096;

    private static readonly Vector3 Forward = Vector3.UnitZ;

    private readonly AccelerationStructureTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TracesRaysAgainstTheInstancesOfATopLevelStructure()
    {
        BottomLevelGeometry quad = _builder.CreateQuad(depth: 0);
        IAccelerationStructure bottomLevel = _builder.CreateBottomLevel();
        IAccelerationStructure topLevel = _builder.CreateTopLevel(2);
        Matrix4x4 near = Matrix4x4.CreateTranslation(0, 0, 5);
        Matrix4x4 far = Matrix4x4.CreateScale(2) * Matrix4x4.CreateTranslation(10, 0, 5);
        _builder.BuildScene([new BottomLevelBuild(bottomLevel, quad)], topLevel, new(near, 7, bottomLevel), new(far, 9, bottomLevel));

        RayHit[] hits = _builder.Trace(topLevel.BindlessIndex, (Vector3.Zero, Forward), (new Vector3(10, 0, 0), Forward), (new Vector3(11.5f, 0, 0), Forward), (new Vector3(5, 0, 0), Forward), (Vector3.Zero, -Forward));

        hits[0].Instance.Should().Be(7, "the ray meets the near quad");
        hits[0].Distance.Should().BeApproximately(5, Precision);
        hits[1].Instance.Should().Be(9, "the ray meets the far quad");
        hits[1].Distance.Should().BeApproximately(5, Precision);
        hits[2].Instance.Should().Be(9, "the far quad is scaled twice as large");
        hits[3].Missed.Should().BeTrue("the ray passes between the quads");
        hits[4].Missed.Should().BeTrue("the quads are ahead of the ray, not behind it");
    }

    [Fact]
    public void LeavesTheHitsOnInstancesThatAreNotOpaqueToTheShader()
    {
        BottomLevelGeometry quad = _builder.CreateQuad(depth: 0);
        IAccelerationStructure bottomLevel = _builder.CreateBottomLevel();
        IAccelerationStructure topLevel = _builder.CreateTopLevel(2);
        Matrix4x4 near = Matrix4x4.CreateTranslation(0, 0, 5);
        Matrix4x4 far = Matrix4x4.CreateTranslation(0, 0, 8);
        _builder.BuildScene([new BottomLevelBuild(bottomLevel, quad)], topLevel, new(near, 1, bottomLevel, opaque: false), new(far, 2, bottomLevel));

        RayHit hit = _builder.Trace(topLevel.BindlessIndex, (Vector3.Zero, Forward))[0];

        hit.Instance.Should().Be(2, "the shader commits none of the near quad's candidate hits, so the ray passes it");
        hit.Distance.Should().BeApproximately(8, Precision);
    }

    [Fact]
    public void GivesOnlyTopLevelStructuresABindlessSlot()
    {
        IAccelerationStructure bottomLevel = _builder.CreateBottomLevel();
        IAccelerationStructure topLevel = _builder.CreateTopLevel(1);

        bottomLevel.BindlessIndex.Should().Be(BindlessIndex.None);
        topLevel.BindlessIndex.Should().NotBe(BindlessIndex.None);
        bottomLevel.Address.Should().NotBe(0UL);
    }

    [Fact]
    public void RefitsABottomLevelStructureToItsVerticesNewPositions()
    {
        BottomLevelGeometry quad = _builder.CreateQuad(depth: 0);
        IAccelerationStructure bottomLevel = _builder.CreateBottomLevel(refittable: true);
        IAccelerationStructure topLevel = _builder.CreateTopLevel(1);
        Matrix4x4 placed = Matrix4x4.CreateTranslation(0, 0, 5);
        _builder.BuildScene([new BottomLevelBuild(bottomLevel, quad)], topLevel, new AccelerationStructureInstance(placed, 3, bottomLevel));
        RayHit before = _builder.Trace(topLevel.BindlessIndex, (Vector3.Zero, Forward))[0];

        AccelerationStructureTestsBuilder.MoveQuad(quad.Vertices, 2);
        _builder.BuildScene([new BottomLevelBuild(bottomLevel, quad, Refit: true)], topLevel, new AccelerationStructureInstance(placed, 3, bottomLevel));
        RayHit after = _builder.Trace(topLevel.BindlessIndex, (Vector3.Zero, Forward))[0];

        before.Distance.Should().BeApproximately(5, Precision);
        after.Instance.Should().Be(3);
        after.Distance.Should().BeApproximately(7, Precision, "the refit follows the vertices two units further");
    }

    [Fact]
    public void KeepsTheScratchOfBuildsItRecordedUntilTheyHaveRun()
    {
        IGraphicsDevice device = _builder.Build();
        BottomLevelGeometry quad = _builder.CreateQuad(depth: 0);
        IAccelerationStructure bottomLevel = _builder.CreateBottomLevel(refittable: true);
        IAccelerationStructure topLevel = _builder.CreateTopLevel(LargeScene);
        Matrix4x4 placed = Matrix4x4.CreateTranslation(0, 0, 5);
        AccelerationStructureInstance instance = new(placed, 3, bottomLevel);
        _builder.BuildScene([new BottomLevelBuild(bottomLevel, quad)], topLevel, instance);

        // The refit takes little scratch and the large scene's build more, so the list takes a larger scratch between them.
        AccelerationStructureTestsBuilder.MoveQuad(quad.Vertices, 2);
        ICommandList commands = _builder.RecordScene([new BottomLevelBuild(bottomLevel, quad, Refit: true)], topLevel, instance);
        int recorded = device.GetInfo().Allocations;
        device.WaitIdle();
        int afterEarlierWork = device.GetInfo().Allocations;
        _builder.Submit(commands);
        RayHit hit = _builder.Trace(topLevel.BindlessIndex, (Vector3.Zero, Forward))[0];

        afterEarlierWork.Should().Be(recorded, "the refit's scratch is kept until the list that recorded it has run");
        hit.Distance.Should().BeApproximately(7, Precision);
    }

    [Fact]
    public void HitsNothingInAnEmptyTopLevelStructureOrTheEmptySlot()
    {
        IAccelerationStructure topLevel = _builder.CreateTopLevel(1);
        _builder.BuildScene([], topLevel);

        RayHit inEmpty = _builder.Trace(topLevel.BindlessIndex, (Vector3.Zero, Forward))[0];
        RayHit inPlaceholder = _builder.Trace(BindlessIndex.None, (Vector3.Zero, Forward))[0];

        inEmpty.Missed.Should().BeTrue();
        inPlaceholder.Missed.Should().BeTrue("slot 0 holds an empty structure, as every table's slot 0 holds a placeholder");
    }

    [Fact]
    public void LeavesRayQueriesOutWhenAskedTo()
    {
        IGraphicsDevice device = _builder.WithoutRayQueries().Build();

        Action create = () => device.CreateBottomLevel(new BottomLevelDescription(3, 3));

        device.Features.Should().NotHaveFlag(GraphicsFeatures.RayQueries);
        create.Should().Throw<NotSupportedException>();
    }
}
