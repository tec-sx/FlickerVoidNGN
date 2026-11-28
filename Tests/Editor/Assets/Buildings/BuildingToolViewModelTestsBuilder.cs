using System.Numerics;

using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Buildings;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

/// <summary>
/// Builds the building creator's Tool tab over the plan tools of a new building's document, opening the document again after an undo
/// or a cancel as the editor does; the view is a fixed camera place.
/// </summary>
internal sealed class BuildingToolViewModelTestsBuilder : IDisposable
{
    private readonly BuildingDocumentTestsBuilder _files = new();

    public BuildingToolViewModelTestsBuilder()
    {
        Document = _files.Build();
        _files.Content.Restored += _ => Document = _files.Build();
        UndoHistory history = _files.Content.GetHistory(BuildingDocumentTestsBuilder.House);
        Editor = new BuildingEditor(new EditorCamera(), static () => Matrix4x4.Identity, () => Document.Building, (_, edit) => Document.Change(edit), history);
    }

    public BuildingDocument Document { get; private set; }

    public BuildingEditor Editor { get; }

    public List<BuildingShot> LookedThrough { get; } = [];

    public (Vector3 Position, Quaternion Rotation) View { get; set; } = (new Vector3(1, 8, -9), Quaternion.Identity);

    public BuildingToolViewModel Build() => new(Editor, () => Document.Building, () => View, LookedThrough.Add);

    public BuildingFile Load() => _files.Load();

    public void Dispose() => _files.Dispose();
}
