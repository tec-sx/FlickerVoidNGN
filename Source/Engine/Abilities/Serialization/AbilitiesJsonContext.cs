using System.Text.Json.Serialization;

using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Abilities.Costs;
using FlickerVoid.Engine.Abilities.Definitions;
using FlickerVoid.Engine.Abilities.Rules;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Abilities.Serialization;

/// <summary>Source-generated JSON for ability, ability set and tag relationship definitions, the costs, the ability rules and the <c>abilities</c> component.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(AbilityDefinition))]
[JsonSerializable(typeof(AbilitySetDefinition))]
[JsonSerializable(typeof(AbilityTagRelationshipsDefinition))]
[JsonSerializable(typeof(AttributeCost))]
[JsonSerializable(typeof(ActivateAbilityEffect))]
[JsonSerializable(typeof(CanActivateAbilityCondition))]
[JsonSerializable(typeof(GrantedAbilities))]
public sealed partial class AbilitiesJsonContext : JsonSerializerContext;
