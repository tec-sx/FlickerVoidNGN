using System.Numerics;

using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Buildings;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

/// <summary>
/// Builds the building creator's plan tools over a new building's document, seen by a camera from over its back wall, with the
/// mouse's place worked out from points of the building; what the tools warn of is kept.
/// </summary>
internal sealed class BuildingEditorTestsBuilder : IDisposable
{
    public const int Width = 1280;
    public const int Height = 800;

    private readonly BuildingDocumentTestsBuilder _files = new();

    public BuildingEditorTestsBuilder()
    {
        Document = _files.Build();
        _files.Content.Restored += _ => Document = _files.Build();
        UndoHistory history = _files.Content.GetHistory(BuildingDocumentTestsBuilder.House);
        Camera.LookAt(new Vector3(0, 1, 0), 0, -0.6f, 16);
        Editor = new BuildingEditor(Camera, static () => Matrix4x4.Identity, () => Document.Building, (_, edit) => Document.Change(edit), history);
        Editor.Warned += Warnings.Add;
    }

    public EditorCamera Camera { get; } = new();

    public BuildingDocument Document { get; private set; }

    public BuildingEditor Editor { get; }

    public List<string> Warnings { get; } = [];

    public BuildingFile Building => Document.Building;

    public float Floor => Building.GetFloorHeight(0);

    public BuildingEditorTestsBuilder WithStoreys(int above)
    {
        Document.SetStoreys(above, false);
        return this;
    }

    /// <summary>Looks at the building from steeply above, so no wall stands before its floor.</summary>
    public BuildingEditorTestsBuilder LookDown()
    {
        Camera.LookAt(new Vector3(0, 1, 0), 0, -1.3f, 16);
        return this;
    }

    /// <summary>Where a point of the building shows in the viewport.</summary>
    public Vector2 Pixel(Vector3 point) => Camera.Project(point, Width, Height);

    public bool Press(Vector3 point)
    {
        Vector2 pixel = Pixel(point);
        Editor.Move(pixel, Width, Height);
        return Editor.Press(pixel, Width, Height);
    }

    public void Drag(Vector3 point)
    {
        Vector2 pixel = Pixel(point);
        Editor.Move(pixel, Width, Height);
    }

    public void Release() => Editor.Release();

    public void Dispose() => _files.Dispose();
}
