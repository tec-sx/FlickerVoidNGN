using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>What a vehicle is, which sets the wheel counts it takes.</summary>
public enum VehicleType
{
    [JsonStringEnumMemberName("car")]
    Car,

    [JsonStringEnumMemberName("motorcycle")]
    Motorcycle,
}
