using Arch.Core;

namespace FlickerVoid.Editor.Core.Tests.Shots;

public sealed class ShotOverlayTests : IDisposable
{
    private readonly ShotOverlayTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsNothingWithoutShotsOrAreas()
    {
        _builder.Draw().Vertices.Length.Should().Be(0);
    }

    [Fact]
    public void DrawsEveryTriggerAreasBox()
    {
        _builder.Scene.Add("""{ "name": "Window", "components": { "transform": {}, "triggerArea": { "size": [2, 2, 2] } } }""");

        int vertices = _builder.Draw().Vertices.Length;

        vertices.Should().Be(12 * 6, "twelve edges, each two triangles");
    }

    [Fact]
    public void DrawsTheSelectedShotsRailAndKeys()
    {
        Entity hall = _builder.Scene.AddShot("Hall");
        int unselected = _builder.Draw().Vertices.Length;

        _builder.Scene.Scene.Selection.Select(hall);
        int selected = _builder.Draw().Vertices.Length;
        _builder.Scene.Timeline.SelectKey(1);
        int withKey = _builder.Draw().Vertices.Length;

        unselected.Should().Be(0);
        selected.Should().BeGreaterThan(3 * 36, "the rail's lines and a cube on each key");
        withKey.Should().Be(selected + 6, "the selected key shows where it looks");
    }
}
