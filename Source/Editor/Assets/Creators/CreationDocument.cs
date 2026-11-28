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
        DoorFitter.Bake(Value, id);
        part.Mesh = mesh;
        Reshaped(id);
        Hold();
    }

    /// <summary>Called when the mesh tools gave the part with the id a new solid, before it is held.</summary>
    protected virtual void Reshaped(string id)
    {
    }

    /// <summary>Turns the part's mirror switch on or off, held in the file as one undo step; throws for an id the creation lacks.</summary>
    public void SetMirror(string id, bool mirror)
    {
        SolidPart part = FindPart(id);
        part.Mirror = mirror;
        Hold();
    }

    /// <summary>Whether the part is one a door can be placed on: it has no joint and is no placed door.</summary>
    public virtual bool IsDoorWall(string partId)
    {
        SolidPart? part = TryFindPart(partId);
        return part is not null && part.Joint is null && part.Fit is null;
    }

    public DoorFit? FindDoorFit(string doorId) => TryFindPart(doorId)?.Fit;

    /// <summary>Places a door on its wall and cuts the wall, held in the file as one undo step; the door's id, or null when it can't be made.</summary>
    public string? PlaceDoor(DoorFit fit)
    {
        string? id = DoorFitter.Place(Value, fit);
        if (id is not null)
        {
            Hold();
        }

        return id;
    }

    /// <summary>Gives a placed door new numbers and cuts its wall again, as one undo step.</summary>
    public bool ReplaceDoor(string doorId, DoorFit fit)
    {
        SolidPart door = FindPart(doorId);
        DoorFit? old = door.Fit;
        door.Fit = fit;
        if (!DoorFitter.Rebuild(Value))
        {
            door.Fit = old;
            return false;
        }

        Hold();
        return true;
    }

    /// <summary>Takes a placed door out and closes its wall, as one undo step.</summary>
    public bool RemoveDoor(string doorId)
    {
        if (!DoorFitter.Remove(Value, doorId))
        {
            return false;
        }

        Hold();
        return true;
    }

    private SolidPart? TryFindPart(string id)
    {
        foreach ((string partId, SolidPart part) in Value.GetParts())
        {
            if (partId == id)
            {
                return part;
            }
        }

        return null;
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
