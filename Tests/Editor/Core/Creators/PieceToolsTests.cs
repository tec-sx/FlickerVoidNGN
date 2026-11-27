using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Tests.Scenes;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Creators;

public sealed class PieceToolsTests : IDisposable
{
    private const string Hillside = """
        { "entities": [
          { "name": "Hill", "components": { "transform": { "position": [0, 0, 0] }, "greybox": { "size": [10, 4, 4] } } },
          { "name": "Tunnel", "components": { "transform": { "position": [0, 0, 0] }, "greybox": { "size": [2, 2, 6] } } },
          { "name": "Far", "components": { "transform": { "position": [40, 0, 0] }, "greybox": { "size": [1, 1, 1] } } }
        ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new();

    public PieceToolsTests()
    {
        _builder.WithScene("Hillside.fvscene", Hillside).Open("Hillside.fvscene");
    }

    private EditorScene Scene => _builder.Scene;

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void CutsOneGreyboxOutOfAnotherWhereTheyOverlapAndLeavesTheCutter()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        Entity hill = Scene.Find("Hill")!.Value;
        Entity tunnel = Scene.Find("Tunnel")!.Value;

        bool cut = PieceTools.Cut(Scene, hill, [tunnel]);

        cut.Should().BeTrue();
        World world = Scene.Game.World;
        SolidMesh? shape = world.Get<Greybox>(hill).Mesh;
        shape.Should().NotBeNull();
        shape!.Faces.Count.Should().BeGreaterThan(6, "the hill has a tunnel through it");
        world.IsAlive(tunnel).Should().BeTrue();
        world.Get<Greybox>(tunnel).Mesh.Should().BeNull("the cutter is left as it was");
        Scene.Game.World.Add<Engine.Rendering.Extraction.Hidden>(tunnel);
        Ray through = new(new Vector3(0, 1, 10), -Vector3.UnitZ);
        Scene.Picker.Pick(world, through).Should().BeNull("the ray passes through the tunnel");
    }

    [Fact]
    public void TheCutIsOneUndoStepOfTheLevel()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        Entity hill = Scene.Find("Hill")!.Value;
        PieceTools.Cut(Scene, hill, [Scene.Find("Tunnel")!.Value]);

        Scene.History.Undo();

        Scene.Game.World.Get<Greybox>(hill).Mesh.Should().BeNull();
    }

    [Fact]
    public void CutsNothingWhenTheCutterDoesNotTouch()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        Entity hill = Scene.Find("Hill")!.Value;

        bool cut = PieceTools.Cut(Scene, hill, [Scene.Find("Far")!.Value]);

        cut.Should().BeFalse();
        Scene.Game.World.Get<Greybox>(hill).Mesh.Should().BeNull();
        Scene.History.CanUndo.Should().BeFalse("nothing changed");
    }
}
