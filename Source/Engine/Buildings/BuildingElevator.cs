using System.Text.Json.Serialization;

using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// An elevator: a platform in a shaft through the floors from one storey to another, with a call button beside the shaft on each
/// storey it stops at. Standing on the platform, a rider takes it up or down a storey; a call brings it to the caller's storey.
/// </summary>
public sealed class BuildingElevator
{
    public int Id { get; set; }

    /// <summary>The middle of the shaft across the floor (X).</summary>
    public float X { get; set; }

    /// <summary>The middle of the shaft across the floor (Z).</summary>
    public float Z { get; set; }

    /// <summary>The platform's width and depth.</summary>
    public float Size { get; set; } = 1.6f;

    /// <summary>The way the platform is stepped onto, in degrees turned from -Z toward +X; the call buttons stand on that side.</summary>
    public float Heading { get; set; }

    /// <summary>The lowest storey it stops at.</summary>
    public int From { get; set; }

    /// <summary>The highest storey it stops at.</summary>
    public int To { get; set; }

    /// <summary>How fast the platform travels, in metres a second.</summary>
    public float Speed { get; set; } = 1.5f;

    /// <summary>Whether each storey it stops at has a button that calls it.</summary>
    public bool CallButtons { get; set; } = true;

    /// <summary>Whether a railing stands round the shaft on the storeys above the lowest, but for the side it is stepped onto.</summary>
    public bool Railing { get; set; } = true;

    /// <summary>The platform's solid, which the creator makes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidPart? Platform { get; set; }

    /// <summary>The railings' solid, which the creator makes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidPart? RailingPart { get; set; }

    public BuildingElevator Copy() => (BuildingElevator)MemberwiseClone();
}
