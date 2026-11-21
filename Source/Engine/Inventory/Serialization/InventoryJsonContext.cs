using System.Text.Json.Serialization;

using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Pickups;
using FlickerVoid.Engine.Inventory.Rules;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Inventory.Serialization;

/// <summary>Source-generated JSON for item definitions, their fragments, the inventory conditions and effects, and the components scenes hold.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(ItemDefinition))]
[JsonSerializable(typeof(UsableFragment))]
[JsonSerializable(typeof(ChargesFragment))]
[JsonSerializable(typeof(ValueFragment))]
[JsonSerializable(typeof(HasItemCondition))]
[JsonSerializable(typeof(HasItemWithTagCondition))]
[JsonSerializable(typeof(HasMoneyCondition))]
[JsonSerializable(typeof(GiveItemEffect))]
[JsonSerializable(typeof(TakeItemEffect))]
[JsonSerializable(typeof(GiveMoneyEffect))]
[JsonSerializable(typeof(TakeMoneyEffect))]
[JsonSerializable(typeof(PickUpEffect))]
[JsonSerializable(typeof(UseInPlaceEffect))]
[JsonSerializable(typeof(Inventories.Inventory))]
[JsonSerializable(typeof(Pickup))]
public sealed partial class InventoryJsonContext : JsonSerializerContext;
