using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Curves;

/// <summary>A value a curve passes through at a time, and how it leaves for the next key.</summary>
public struct CurveKey
{
    public float Time { get; set; }

    public float Value { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public CurveMode Mode { get; set; }

    /// <summary>The slope a free key is arrived at, in value per time.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float In { get; set; }

    /// <summary>The slope a free key is left at, in value per time.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Out { get; set; }
}
