using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>Which of a vehicle's axles its engine turns.</summary>
public enum DrivenAxles
{
    /// <summary>Every axle but the first.</summary>
    [JsonStringEnumMemberName("rear")]
    Rear,

    /// <summary>The first axle only.</summary>
    [JsonStringEnumMemberName("front")]
    Front,

    [JsonStringEnumMemberName("all")]
    All,
}
