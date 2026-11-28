using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

/// <summary>A component holding a tag and a list of tags, as module components do.</summary>
public struct Lock
{
    public Lock()
    {
    }

    public Tag Key { get; set; }

    public List<Tag> Opens { get; set; } = [];

    /// <summary>A name that looks like a tag but is not one.</summary>
    public string Label { get; set; } = "";
}

/// <summary>A data asset holding tags deep in polymorphic values, beside a text whose key looks like a tag.</summary>
public sealed class Errand
{
    public LocalizedText Title { get; set; } = new();

    public List<Step> Steps { get; set; } = [];

    public Dictionary<string, Tag> Marks { get; set; } = [];
}

public abstract class Step;

public sealed class FetchStep : Step
{
    public Tag Item { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(Lock))]
[JsonSerializable(typeof(Errand))]
[JsonSerializable(typeof(FetchStep))]
internal sealed partial class ContentTagsTestsJsonContext : JsonSerializerContext;
