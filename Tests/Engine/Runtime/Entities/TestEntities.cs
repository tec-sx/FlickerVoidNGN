using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

/// <summary>A component with a default other than zero, to see that spawned classes start at their components' defaults.</summary>
public struct Glow
{
    public Glow()
    {
    }

    public float Intensity { get; set; } = 1;
}

/// <summary>An entity class without a template: a placed glow.</summary>
[EntityClass]
public readonly partial struct Lamp
{
    public partial ref Transform Transform { get; }

    public partial ref Glow Glow { get; }
}

/// <summary>An entity class that spawns from its template.</summary>
[EntityClass(Template = "Props/Lantern.fventity")]
public readonly partial struct Lantern
{
    public partial ref Glow Glow { get; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(Glow))]
internal sealed partial class TestEntitiesJsonContext : JsonSerializerContext;
