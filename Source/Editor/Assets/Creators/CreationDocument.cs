using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Assets.Creators;

/// <summary>
/// A creation's file being edited in its creator: each change is held unsaved, one undo step in the file's history, and reaches every
/// placed copy through the change hot reload reports. What every kind shares is here: a part's solid, its mirror switch and doors.
/// </summary>
public abstract class CreationDocument<TFile>(UnsavedContent content, string path, JsonTypeInfo<TFile> type)
    : ContentDocument<TFile>(content, path, type), ICreationDocument
    where TFile : class, ICreationFile
{
    public ICreationFile File => Value;

    /// <summary>Gives the part with the id a new solid, such as one a mesh tool made; throws for an id the creation lacks.</summary>
    public void SetMesh(string id, SolidMesh mesh)
    {
        SolidPart part = FindPart(id);
        part.Mesh = mesh;
        Hold();
    }

    /// <summary>Turns the part's mirror switch on or off, held in the file as one undo step; throws for an id the creation lacks.</summary>
    public void SetMirror(string id, bool mirror)
    {
        SolidPart part = FindPart(id);
        part.Mirror = mirror;
        Hold();
    }

    /// <summary>Hangs a door in the face of the part, hinged on the edge, held in the file as one undo step; the new door's id, or null when it can't be made.</summary>
    public string? HangDoor(string partId, int face, SolidEdge hinge, DoorOptions options)
    {
        string? id = DoorTool.Hang(Value, partId, face, hinge, options);
        if (id is not null)
        {
            Hold();
        }

        return id;
    }

    private SolidPart FindPart(string id)
    {
        foreach ((string partId, SolidPart part) in Value.GetParts())
        {
            if (partId == id)
            {
                return part;
            }
        }

        throw new ArgumentException($"The creation has no part {id}.", nameof(id));
    }
}
