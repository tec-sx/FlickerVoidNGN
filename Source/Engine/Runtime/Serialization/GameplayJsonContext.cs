using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Source-generated JSON for the gameplay rules data files hold: condition sets, effect lists and the fact conditions and effects.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConditionSet))]
[JsonSerializable(typeof(EffectList))]
[JsonSerializable(typeof(FactCondition))]
[JsonSerializable(typeof(FactEffect))]
[JsonSerializable(typeof(GroupCondition))]
public sealed partial class GameplayJsonContext : JsonSerializerContext;
