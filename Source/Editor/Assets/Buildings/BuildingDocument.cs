using System.Numerics;

using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;

namespace FlickerVoid.Editor.Assets.Buildings;

/// <summary>A building file being edited in the creator: each change is held unsaved, one undo step in the file's history.</summary>
public sealed class BuildingDocument(UnsavedContent content, string path)
    : CreationDocument<BuildingFile>(content, path, BuildingsJsonContext.Default.BuildingFile)
{
    public BuildingFile Building => Value;

    /// <summary>Gives the building that many storeys on the ground and a cellar or none; throws for a count it doesn't take.</summary>
    public void SetStoreys(int above, bool cellar)
    {
        BuildingTools.SetStoreys(Building, above, cellar);
        Hold();
    }

    /// <summary>Adds a flight of stairs from the level to the one above, opening that floor; its id, or null when no storey is above.</summary>
    public string? AddStairs(int level)
    {
        Vector2 foot = BuildingTools.GetStairsFoot(Building, level);
        string? id = BuildingTools.AddStairs(Building, level, foot, BuildingTools.StairsRun);
        if (id is not null)
        {
            Hold();
        }

        return id;
    }

    /// <summary>Gives the building an elevator stopping at every storey, with its shaft cut through the floors.</summary>
    public void AddElevator()
    {
        Vector2 place = BuildingTools.GetElevatorPlace(Building);
        BuildingTools.AddElevator(Building, place);
        Hold();
    }

    /// <summary>Takes the elevator out; the shaft stays open, as the mesh tools close it.</summary>
    public void RemoveElevator()
    {
        Building.Elevator = null;
        Hold();
    }
}
