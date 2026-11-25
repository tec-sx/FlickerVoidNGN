using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>A vehicle placed in a level: the file it is made from, whose parts are spawned under the entity.</summary>
public struct Vehicle
{
    [ContentFile(VehicleFile.Extension)]
    public required string File { get; set; }
}
