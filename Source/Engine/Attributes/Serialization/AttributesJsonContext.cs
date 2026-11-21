using System.Text.Json.Serialization;

using FlickerVoid.Engine.Attributes.Checks;
using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Rules;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Attributes.Serialization;

/// <summary>Source-generated JSON for attribute, modifier and check definitions, the attribute conditions and effects, and the <c>attributes</c> component.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(AttributeDefinition))]
[JsonSerializable(typeof(ModifierDefinition))]
[JsonSerializable(typeof(CheckDefinition))]
[JsonSerializable(typeof(AttributeCondition))]
[JsonSerializable(typeof(PassiveCheckCondition))]
[JsonSerializable(typeof(ModifyAttributeEffect))]
[JsonSerializable(typeof(SetAttributeEffect))]
[JsonSerializable(typeof(ApplyModifierEffect))]
[JsonSerializable(typeof(RemoveModifierEffect))]
[JsonSerializable(typeof(AttributeSet))]
public sealed partial class AttributesJsonContext : JsonSerializerContext;
