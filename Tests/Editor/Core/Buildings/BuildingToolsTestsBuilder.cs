using FlickerVoid.Engine.Buildings;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

/// <summary>Builds a new building with a number of storeys on the ground for the building tools.</summary>
internal sealed class BuildingToolsTestsBuilder
{
    private int _storeys = 1;

    public BuildingToolsTestsBuilder WithStoreys(int storeys)
    {
        _storeys = storeys;
        return this;
    }

    public BuildingFile Build()
    {
        BuildingFile building = BuildingFile.Create();
        building.SetStoreys(_storeys, false);
        return building;
    }
}
