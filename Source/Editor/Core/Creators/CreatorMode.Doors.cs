using System.Numerics;

using Arch.Core;
using Arch.Core.Extensions;

using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Creators;

public sealed partial class CreatorMode
{
    /// <summary>The creation's doors, set by the shell; without it the door tool is off.</summary>
    public IDoorHost? Doors { get; set; }

    /// <summary>The door tool's work in progress, or null when no door is being placed or changed.</summary>
    public DoorSession? Door { get; private set; }

    /// <summary>Raised when the door tool starts, ends or changes a number.</summary>
    public event Action? DoorChanged;

    /// <summary>Whether the creation takes doors at all: it is no level piece and the shell gave it its doors.</summary>
    public bool HasDoorTool => Doors is not null && Placement != CreatorPlacement.Piece;

    /// <summary>Whether the door tool can start: the creation is not a piece and Manifold cuts.</summary>
    public bool CanPlaceDoor => Doors is { IsAvailable: true } && Placement != CreatorPlacement.Piece;

    /// <summary>Starts the door tool (D): a door follows the mouse onto the walls, a click sets it. Leaves the mesh and any door being changed.</summary>
    public bool BeginDoor()
    {
        if (!CanPlaceDoor)
        {
            return false;
        }

        LeaveMesh();
        CancelDoor();
        DoorFit fit = new() { Wall = string.Empty };
        Door = new DoorSession(_camera, fit, null, Matrix4x4.Identity);
        Door.Changed += RaiseDoorChanged;
        DoorChanged?.Invoke();
        return true;
    }

    /// <summary>Opens the placed door with the id for changing, as the click on its part does; false for a part that is not a placed door.</summary>
    public bool EditDoor(string doorId)
    {
        if (Doors?.FindFit(doorId) is not { } stored || Doors.IsWall(doorId))
        {
            return false;
        }

        Matrix4x4? toWorld = FindWallToWorld(stored.Wall);
        if (toWorld is not { } placed)
        {
            return false;
        }

        DoorFit copy = stored.Copy();
        Door = new DoorSession(_camera, copy, doorId, placed);
        Door.Changed += RaiseDoorChanged;
        DoorChanged?.Invoke();
        return true;
    }

    /// <summary>Makes the door as it stands: a new one is placed and cut, a changed one cut again, as one undo step; false when it is not set on a wall or can't be cut.</summary>
    public bool ApplyDoor()
    {
        if (Door is not { IsPlaced: true } door || Doors is not { } host)
        {
            return false;
        }

        string? id;
        if (door.DoorId is { } existing)
        {
            if (!host.Replace(existing, door.Fit))
            {
                return false;
            }

            id = existing;
        }
        else
        {
            id = host.Place(door.Fit);
            if (id is null)
            {
                return false;
            }
        }

        EndDoor();
        SelectedPart = id;
        PartSelected?.Invoke();
        return true;
    }

    /// <summary>Drops the door being placed or changed, leaving the file as it was.</summary>
    public bool CancelDoor()
    {
        if (Door is null)
        {
            return false;
        }

        EndDoor();
        return true;
    }

    /// <summary>Takes the placed door being changed out of the creation, closing its wall; false for a door not yet applied.</summary>
    public bool RemoveDoor()
    {
        if (Door is not { DoorId: { } id } || Doors is not { } host || !host.Remove(id))
        {
            return false;
        }

        EndDoor();
        SelectPart(null);
        return true;
    }

    /// <summary>Adds the door's marks to the overlay.</summary>
    public void DrawDoor(OverlayBuilder overlay) => Door?.Draw(overlay);

    private void EndDoor()
    {
        if (Door is not { } door)
        {
            return;
        }

        door.Changed -= RaiseDoorChanged;
        Door = null;
        DoorChanged?.Invoke();
    }

    private void RaiseDoorChanged() => DoorChanged?.Invoke();

    private bool PressDoor(Vector2 pixel, int width, int height)
    {
        if (Door is not { } door)
        {
            return false;
        }

        if (door.IsPlaced && door.BeginDrag(pixel, width, height))
        {
            return true;
        }

        AimDoor(pixel, width, height);
        door.Place();
        DoorChanged?.Invoke();
        return true;
    }

    private void AimDoor(Vector2 pixel, int width, int height)
    {
        if (Door is not { } door || Doors is not { } host || _findMesh is null)
        {
            return;
        }

        World world = _scene.Game.World;
        Ray ray = _camera.GetRay(pixel, width, height);
        if (_scene.Picker.PickPart(world, ray, Root) is not { } picked)
        {
            return;
        }

        string id = world.Get<CreationPart>(picked).Id;
        SolidMesh? solid = _findMesh(id);
        if (solid is null || !host.IsWall(id))
        {
            return;
        }

        Matrix4x4 toWorld = world.Get<Transform>(picked).ToMatrix();
        door.Aim(id, solid, toWorld, pixel, width, height);
    }

    private void MoveDoor(Vector2 pixel, int width, int height)
    {
        if (Door is not { } door)
        {
            return;
        }

        if (door.IsDragging || door.IsPlaced)
        {
            door.Move(pixel, width, height);
            return;
        }

        AimDoor(pixel, width, height);
    }

    private Matrix4x4? FindWallToWorld(string wallId)
    {
        CreationParts? parts = _scene.Game.Services.GetService<CreationParts>();
        if (parts is null)
        {
            return null;
        }

        Entity wall = parts.Find(Root, wallId);
        if (wall == Entity.Null)
        {
            return null;
        }

        return _scene.Game.World.Get<Transform>(wall).ToMatrix();
    }
}
