using System.Numerics;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Curves;

/// <summary>A color a color curve passes through at a time, and how it leaves for the next key.</summary>
public struct ColorKey
{
    public float Time { get; set; }

    public Vector3 Color { get; set; }

    /// <summary>A color has no slopes of its own, so a free key is a smooth one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public CurveMode Mode { get; set; }
}
