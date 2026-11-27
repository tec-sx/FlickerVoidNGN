using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Buildings;

/// <summary>A building placed in a level: the file it is made from, whose parts are spawned under the entity.</summary>
public struct Building
{
    [ContentFile(BuildingFile.Extension)]
    public required string File { get; set; }
}
