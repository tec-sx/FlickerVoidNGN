using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Curves;

/// <summary>How a curve travels from a key to the next one.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CurveMode>))]
public enum CurveMode
{
    /// <summary>Eases through the key at a slope found from its neighbours, never passing above or below them.</summary>
    Smooth,

    /// <summary>A straight line to the next key.</summary>
    Linear,

    /// <summary>Holds the key's value until the next key.</summary>
    Constant,

    /// <summary>Arrives and leaves at the key's own slopes.</summary>
    Free,
}
