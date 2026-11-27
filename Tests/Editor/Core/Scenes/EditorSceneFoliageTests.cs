using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Foliage;
using FlickerVoid.Engine.Foliage.Serialization;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneFoliageTests : IDisposable
{
    private const string Oak = "Plants/Oak.fvfoliage";
    private const string Park = """
        { "entities": [
          { "name": "Oak", "components": { "transform": {}, "foliage": { "file": "Plants/Oak.fvfoliage" } } },
          { "name": "Grove", "components": { "transform": { "position": [30, 0, 0] }, "foliageScatter": { "file": "Plants/Oak.fvfoliage", "count": 3 } } }
        ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsTheScenesPlantsAndScattersAndPicksAPlantByItsCrown()
    {
        FoliageFile oak = FoliageFile.Create(FoliageKind.Tree);
        string json = JsonSerializer.Serialize(oak, FoliageJsonContext.Default.FoliageFile);
        _builder.WithFile(Oak, json).WithScene("Park.fvscene", Park).Open("Park.fvscene");
        EditorScene scene = _builder.Scene;
        Entity placed = scene.Find("Oak")!.Value;
        Entity grove = scene.Find("Grove")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        Ray down = new(new Vector3(0, 20, 0), -Vector3.UnitY);

        Entity? picked = scene.Picker.Pick(scene.Game.World, down);

        parts.FindAll(placed).Should().HaveCount(10);
        parts.FindAll(grove).Should().HaveCount(30, "three copies of ten parts");
        picked.Should().Be(placed);
    }

    [Theory]
    [InlineData(false, "foliage")]
    [InlineData(true, "foliageScatter")]
    public void AFoliagePresetPlacesOnePlantOrAScatter(bool scattered, string component)
    {
        EntityPreset preset = EntityPreset.ForFoliage(Oak, scattered);

        preset.Name.Should().Be("Oak");
        preset.Components.Should().Equal("transform", component);
        preset.Icon.Should().Be("Tree");
    }
}
