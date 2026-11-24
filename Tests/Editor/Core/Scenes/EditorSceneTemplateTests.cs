using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneTemplateTests : IDisposable
{
    private const string Room = """
        { "entities": [
          { "name": "Lamp", "components": { "transform": { "position": [1, 2, 3] }, "pointLight": { "range": 7 } } },
          { "name": "Box", "components": { "transform": {} } }
        ] }
        """;

    private const string Glow = """{ "components": { "pointLight": { "range": 3 } } }""";

    private readonly EditorSceneTestsBuilder _builder = new();

    public EditorSceneTemplateTests()
    {
        _builder.WithScene("Templates/Glow.fventity", Glow).WithScene("Room.fvscene", Room).Open("Room.fvscene");
    }

    public void Dispose() => _builder.Dispose();

    private World World => _builder.Scene.Game.World;

    private Entity Named(string name) => _builder.Scene.Find(name)!.Value;

    [Fact]
    public void SavingAnEntityAsATemplateWritesItsComponentsButItsPlaceAndMakesItStartFromIt()
    {
        bool saved = _builder.Scene.SaveAsTemplate(Named("Lamp"), "Templates/Lamp.fventity");
        _builder.Scene.Save();

        saved.Should().BeTrue();
        string template = _builder.ReadScene("Templates/Lamp.fventity");
        template.Should().Contain("pointLight").And.Contain("\"range\": 7").And.NotContain("transform");
        template.Should().Contain("\"$schema\": \"../.schemas/entity.schema.json\"");
        _builder.Scene.GetTemplate(Named("Lamp")).Should().Be("Templates/Lamp.fventity");
        string scene = _builder.ReadScene("Room.fvscene");
        scene.Should().Contain("\"template\":\"Templates/Lamp.fventity\"").And.NotContain("pointLight");
    }

    [Fact]
    public void RefusesToSaveOverAFileThatExists()
    {
        bool saved = _builder.Scene.SaveAsTemplate(Named("Lamp"), "Templates/Glow.fventity");

        saved.Should().BeFalse();
        _builder.ReadScene("Templates/Glow.fventity").Should().Be(Glow);
    }

    [Fact]
    public void StartingFromATemplateGivesTheComponentsItLacksAndUndoTakesThemOff()
    {
        _builder.Scene.SetTemplate(Named("Box"), "Templates/Glow.fventity");
        float range = World.Get<PointLight>(Named("Box")).Range;
        string? template = _builder.Scene.GetTemplate(Named("Box"));
        _builder.Scene.History.Undo();

        range.Should().Be(3);
        template.Should().Be("Templates/Glow.fventity");
        World.Has<PointLight>(Named("Box")).Should().BeFalse();
        _builder.Scene.GetTemplate(Named("Box")).Should().BeNull();
    }

    [Fact]
    public void AnEntityKeepsItsOwnValuesOverItsNewTemplatesAndSavesThemAsDifferences()
    {
        _builder.Scene.SetTemplate(Named("Lamp"), "Templates/Glow.fventity");
        _builder.Scene.Save();

        World.Get<PointLight>(Named("Lamp")).Range.Should().Be(7);
        World.Get<Transform>(Named("Lamp")).Position.Should().Be(new Vector3(1, 2, 3));
        _builder.ReadScene("Room.fvscene").Should().Contain("\"template\":\"Templates/Glow.fventity\"").And.Contain("\"range\":7");
    }

    [Fact]
    public void StartingFromNoTemplateKeepsEverythingAndSavesItInFull()
    {
        _builder.Scene.SetTemplate(Named("Box"), "Templates/Glow.fventity");

        _builder.Scene.SetTemplate(Named("Box"), null);
        _builder.Scene.Save();

        World.Get<PointLight>(Named("Box")).Range.Should().Be(3);
        string scene = _builder.ReadScene("Room.fvscene");
        scene.Should().NotContain("template").And.Contain("\"range\":3");
    }
}
