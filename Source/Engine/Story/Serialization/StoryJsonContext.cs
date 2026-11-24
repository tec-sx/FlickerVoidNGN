using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;

namespace FlickerVoid.Engine.Story.Serialization;

/// <summary>Source-generated JSON for quests, knowledge, their conditions and effects, and the quest flow nodes.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(QuestDefinition))]
[JsonSerializable(typeof(QuestStateCondition))]
[JsonSerializable(typeof(SetQuestStateEffect))]
[JsonSerializable(typeof(SetQuestStateNode))]
[JsonSerializable(typeof(CompleteObjectiveNode))]
[JsonSerializable(typeof(WaitForQuestStateNode))]
[JsonSerializable(typeof(SavedQuestFlows))]
[JsonSerializable(typeof(KnowledgeDefinition))]
[JsonSerializable(typeof(KnowsCondition))]
[JsonSerializable(typeof(LearnEffect))]
public sealed partial class StoryJsonContext : JsonSerializerContext;
