using Arch.Core;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Runtime.Greyboxes;

namespace FlickerVoid.Editor.Tests.Viewports;

public sealed class ViewportViewModelPieceTests : IDisposable
{
    private const string Hillside = """
        { "entities": [
          { "name": "Hill", "components": { "transform": {}, "greybox": { "size": [10, 4, 4] } } },
          { "name": "Tunnel", "components": { "transform": {}, "greybox": { "size": [2, 2, 6] } } },
          { "name": "Lamp", "components": { "transform": { "position": [20, 0, 0] } } }
        ] }
        """;

    private readonly ViewportViewModelTestsBuilder _builder = new();

    public ViewportViewModelPieceTests()
    {
        _builder.OpenScene(Hillside);
    }

    private ViewportViewModel Viewport => _builder.ViewModel;

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShapeShowsForASelectedGreyboxAndAsksToShapeIt()
    {
        Entity hill = Viewport.Scene.Find("Hill")!.Value;
        Entity lamp = Viewport.Scene.Find("Lamp")!.Value;
        List<Entity> asked = [];
        Viewport.PieceRequested += asked.Add;
        bool nothing = Viewport.CanShapeSelected;
        Viewport.Selection.Select(lamp);
        bool onLamp = Viewport.CanShapeSelected;

        Viewport.Selection.Select(hill);
        bool onHill = Viewport.CanShapeSelected;
        Viewport.ShapeSelected();

        nothing.Should().BeFalse();
        onLamp.Should().BeFalse("only greyboxes are shaped");
        onHill.Should().BeTrue();
        Viewport.CanCutSelected.Should().BeFalse("a cut needs other greyboxes to cut with");
        asked.Should().Equal(hill);
    }

    [Fact]
    public void CutTakesTheOtherSelectedGreyboxesOutOfTheOneSelectedLast()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        Entity hill = Viewport.Scene.Find("Hill")!.Value;
        Entity tunnel = Viewport.Scene.Find("Tunnel")!.Value;
        Viewport.Selection.SelectAll([tunnel, hill]);
        bool offered = Viewport.CanCutSelected;

        Viewport.CutSelected();

        offered.Should().BeTrue();
        _builder.World.Get<Greybox>(hill).Mesh.Should().NotBeNull("the hill was selected last, so it is the one cut");
        _builder.World.Get<Greybox>(tunnel).Mesh.Should().BeNull();
        Viewport.Status.Should().StartWith("Cut Hill");
    }
}
