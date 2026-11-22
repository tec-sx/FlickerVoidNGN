using Arch.Core;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>Sent when a conversation ends, by its graph, by <see cref="DialogueSystem.End"/> or because its owner is gone.</summary>
public readonly record struct ConversationEndedEvent(Entity Instigator, Entity Owner);
