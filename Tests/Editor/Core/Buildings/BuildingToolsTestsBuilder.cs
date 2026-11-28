using System.Numerics;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

/// <summary>Builds a new building with a number of storeys on the ground, and a cellar or none, for the building tools.</summary>
internal sealed class BuildingToolsTestsBuilder
{
    private int _storeys = 1;
    private bool _cellar;

    public BuildingToolsTestsBuilder WithStoreys(int storeys)
    {
        _storeys = storeys;
        return this;
    }

    public BuildingToolsTestsBuilder WithCellar()
    {
        _cellar = true;
        return this;
    }

    public BuildingFile Build()
    {
        BuildingFile building = BuildingFile.Create();
        BuildingTools.SetStoreys(building, _storeys, _cellar);
        return building;
    }

    /// <summary>The ground floor of a new building.</summary>
    public static BuildingStorey Ground(BuildingFile building) => building.FindStorey(0)!;

    /// <summary>The wall of the ground floor at the point, with the distance along it.</summary>
    public static (PlanWall Wall, float Along) WallAt(BuildingStorey storey, Vector2 point) => PlanEdits.FindWall(storey, point, 0.2f)!.Value;
}
