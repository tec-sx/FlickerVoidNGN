using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

/// <summary>A component naming an entity of its scene and a content file, as module components do.</summary>
public struct Signpost
{
    public Signpost()
    {
    }

    [EntityReference]
    public string Target { get; set; } = "";

    [ContentFile(".fvdata")]
    public string Note { get; set; } = "";
}

/// <summary>A data asset naming content files deep in polymorphic values.</summary>
public sealed class Journey
{
    public LocalizedText Title { get; set; } = new();

    public List<Leg> Legs { get; set; } = [];
}

public abstract class Leg;

public sealed class WalkLeg : Leg
{
    [ContentFile(".fvscene")]
    public string Scene { get; set; } = "";
}

/// <summary>A table row naming a content file.</summary>
public sealed class FareRow
{
    public int Price { get; set; }

    [ContentFile(".fvdata")]
    public string Journey { get; set; } = "";
}

/// <summary>A file kind a module registers (<c>.fvtrail</c>), holding one value.</summary>
public sealed class Trail
{
    [ContentFile(".fvdata")]
    public string Journey { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(Signpost))]
[JsonSerializable(typeof(Journey))]
[JsonSerializable(typeof(WalkLeg))]
[JsonSerializable(typeof(FareRow))]
[JsonSerializable(typeof(Trail))]
internal sealed partial class ContentCheckTestsJsonContext : JsonSerializerContext;
