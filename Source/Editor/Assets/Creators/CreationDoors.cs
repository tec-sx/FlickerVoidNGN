using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Editor.Assets.Creators;

/// <summary>The door tool's view of the creation being edited: its document, looked up as the creator panel changes.</summary>
public sealed class CreationDoors(Func<ICreationDocument?> document) : IDoorHost
{
    public bool IsAvailable => DoorFitter.IsAvailable && document() is not null;

    public bool IsWall(string partId) => document()?.IsDoorWall(partId) == true;

    public DoorFit? FindFit(string doorId) => document()?.FindDoorFit(doorId);

    public string? Place(DoorFit fit) => document()?.PlaceDoor(fit);

    public bool Replace(string doorId, DoorFit fit) => document()?.ReplaceDoor(doorId, fit) == true;

    public bool Remove(string doorId) => document()?.RemoveDoor(doorId) == true;
}
