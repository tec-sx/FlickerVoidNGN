using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Definitions;

namespace FlickerVoid.Engine.Runtime.Tests.Data;

/// <summary>An item definition as a module would declare one, composed of fragments.</summary>
public sealed class TestItem : Definition
{
    public List<TestItemFragment> Fragments { get; set; } = [];
}

public abstract class TestItemFragment : Fragment;

public sealed class PickupFragment : TestItemFragment
{
    public string Mesh { get; set; } = "";
}

public sealed class UsableFragment : TestItemFragment
{
    public int Uses { get; set; } = 1;
}

/// <summary>Another data asset type, which an item file must not load as.</summary>
public sealed class TestFaction : Definition;

/// <summary>A table row: what something costs.</summary>
public sealed class PriceRow
{
    public int Price { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(TestItem))]
[JsonSerializable(typeof(PickupFragment))]
[JsonSerializable(typeof(UsableFragment))]
[JsonSerializable(typeof(TestFaction))]
[JsonSerializable(typeof(PriceRow))]
internal sealed partial class TestDataJsonContext : JsonSerializerContext;
