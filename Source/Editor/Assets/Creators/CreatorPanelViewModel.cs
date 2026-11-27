using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Assets.Creators;

/// <summary>What every creator's panel gives the creator, whatever it makes: the creation's parts, and the mesh tools' way to its file.</summary>
public abstract partial class CreatorPanelViewModel(ICreationDocument document) : ObservableObject
{
    /// <summary>What the creation is called in the editor: Vehicle, Building.</summary>
    public abstract string Kind { get; }

    /// <summary>The ids of the creation's parts, in its file's order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Parts { get; private set; } = [];

    /// <summary>The solid of the part with the id as the file holds it, or null when the creation has no such part.</summary>
    public SolidMesh? FindMesh(string id) => FindPart(id)?.Mesh;

    /// <summary>Whether the part with the id has its mirror switch on; false for an id the creation lacks.</summary>
    public bool FindMirror(string id) => FindPart(id)?.Mirror == true;

    /// <summary>Hangs a door in the part's face on the edge; the new door's id, or null.</summary>
    public string? HangDoor(string partId, int face, SolidEdge hinge, DoorOptions options) => document.HangDoor(partId, face, hinge, options);

    public void SetMirror(string id, bool mirror) => document.SetMirror(id, mirror);

    /// <summary>Gives the part a new solid, held in the file as one undo step.</summary>
    public void SetMesh(string id, SolidMesh mesh) => document.SetMesh(id, mesh);

    /// <summary>Reads the parts from the file again, after a change to which it has.</summary>
    protected void ShowParts()
    {
        IReadOnlyList<(string Id, SolidPart Part)> parts = document.File.GetParts();
        Parts = [.. parts.Select(static part => part.Id)];
    }

    private SolidPart? FindPart(string id)
    {
        foreach ((string partId, SolidPart part) in document.File.GetParts())
        {
            if (partId == id)
            {
                return part;
            }
        }

        return null;
    }
}
