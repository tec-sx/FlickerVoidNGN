using System.Text.Json.Serialization;

using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Interaction.Serialization;

/// <summary>Source-generated JSON for interactable definitions, the interaction conditions and the components scenes hold.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(InteractableDefinition))]
[JsonSerializable(typeof(InteractorTagsCondition))]
[JsonSerializable(typeof(LockedCondition))]
[JsonSerializable(typeof(Interactable))]
[JsonSerializable(typeof(Interactor))]
[JsonSerializable(typeof(Lockable))]
[JsonSerializable(typeof(Toggle))]
public sealed partial class InteractionJsonContext : JsonSerializerContext;
