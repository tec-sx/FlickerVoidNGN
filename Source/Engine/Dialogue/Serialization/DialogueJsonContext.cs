using System.Text.Json.Serialization;

using FlickerVoid.Engine.Dialogue.Barks;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Dialogue.Serialization;

/// <summary>Source-generated JSON for the dialogue participant, the dialogue nodes, the bark rows and the start conversation effect.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(DialogueParticipant))]
[JsonSerializable(typeof(LineNode))]
[JsonSerializable(typeof(ChoiceNode))]
[JsonSerializable(typeof(BarkNode))]
[JsonSerializable(typeof(BarkRow))]
[JsonSerializable(typeof(StartConversationEffect))]
public sealed partial class DialogueJsonContext : JsonSerializerContext;
