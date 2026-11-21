using System.Text.Json.Serialization;

using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Inventory.Serialization;

/// <summary>Source-generated JSON for item definitions, their fragments and the components scenes hold.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(ItemDefinition))]
[JsonSerializable(typeof(UsableFragment))]
[JsonSerializable(typeof(ChargesFragment))]
[JsonSerializable(typeof(ValueFragment))]
[JsonSerializable(typeof(Inventories.Inventory))]
public sealed partial class InventoryJsonContext : JsonSerializerContext;
