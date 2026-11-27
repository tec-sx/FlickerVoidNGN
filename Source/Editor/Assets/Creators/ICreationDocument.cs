using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Assets.Creators;

/// <summary>A creation's document as every creator's panel uses it, whatever its kind.</summary>
public interface ICreationDocument
{
    string Path { get; }

    ICreationFile File { get; }

    void SetMesh(string id, SolidMesh mesh);

    void SetMirror(string id, bool mirror);

    bool IsDoorWall(string partId);

    DoorFit? FindDoorFit(string doorId);

    string? PlaceDoor(DoorFit fit);

    bool ReplaceDoor(string doorId, DoorFit fit);

    bool RemoveDoor(string doorId);
}
