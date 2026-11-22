using Arch.Core;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>Sent when a conversation starts.</summary>
public readonly record struct ConversationStartedEvent(Entity Instigator, Entity Owner);
