using System.Numerics;
using System.Text;
using System.Text.Json;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

/// <summary>Builds the creator's Walk over a building written to a temporary content folder, with clicks straight down onto points.</summary>
internal sealed class BuildingWalkSessionTestsBuilder : IDisposable
{
    public const string File = "Buildings/Inn.fvbuilding";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidBuildingWalkSessionTests", Guid.NewGuid().ToString("N"));
    private BuildingWalk? _walk;

    public BuildingWalkSessionTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        Content = new ContentManager(_folder);
    }

    public ContentManager Content { get; }

    public BuildingWalk Build(BuildingFile building)
    {
        string json = JsonSerializer.Serialize(building, BuildingsJsonContext.Default.BuildingFile);
        Content.Write(File, Encoding.UTF8.GetBytes(json));
        _walk = new BuildingWalk(Content, File, building);
        return _walk;
    }

    /// <summary>A ray straight down onto the point from above it.</summary>
    public static Ray Down(Vector3 point) => new(point + new Vector3(0, 20, 0), -Vector3.UnitY);

    /// <summary>A ray onto the point from a little in front and above, as a camera outside looks at a door.</summary>
    public static Ray Toward(Vector3 point, Vector3 from) => new(from, Vector3.Normalize(point - from));

    public Vector3 PlaceOf(BuildingWalk walk) => walk.World.Get<Transform>(walk.Walker).Position;

    public void Dispose()
    {
        _walk?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
