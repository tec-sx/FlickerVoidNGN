using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>Adds vehicles to a game.</summary>
public static class VehiclesGameExtensions
{
    /// <summary>Adds the <c>.fvvehicle</c> file kind and its schema, so the content check reads vehicle files.</summary>
    public static void AddVehicles(this Game game)
    {
        game.Schemas.AddFile("vehicle", VehicleFile.Extension, static () => VehiclesJsonContext.Default.VehicleFile);
    }
}
