using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>Sent when a quest starts, completes or fails, or one of its objectives is done.</summary>
public readonly record struct QuestChangedEvent(Tag Quest, QuestState State);
