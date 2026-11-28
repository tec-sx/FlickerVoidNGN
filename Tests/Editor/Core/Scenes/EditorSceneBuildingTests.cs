using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneBuildingTests : IDisposable
{
    private const string House = "Houses/House.fvbuilding";
    private const string Street = """
        { "entities": [ { "name": "House", "components": { "transform": {}, "building": { "file": "Houses/House.fvbuilding" } } } ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsTheScenesBuildingsAndPicksEachByItsRoof()
    {
        BuildingFile house = BuildingFile.Create();
        BuildingTools.SetStoreys(house, 2, true);
        BuildingTools.Rebuild(house);
        string json = JsonSerializer.Serialize(house, BuildingsJsonContext.Default.BuildingFile);
        _builder.WithFile(House, json).WithScene("Street.fvscene", Street).Open("Street.fvscene");
        EditorScene scene = _builder.Scene;
        Entity placed = scene.Find("House")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        Ray down = new(new Vector3(0, 20, 0), -Vector3.UnitY);

        Entity? picked = scene.Picker.Pick(scene.Game.World, down);

        IEnumerable<string> ids = parts.FindAll(placed).Select(part => scene.Game.World.Get<CreationPart>(part).Id);
        ids.Should().Contain(["cellar", "storey.0", "storey.1", "storey.0.walls", "roof"]);
        picked.Should().Be(placed);
    }
}
