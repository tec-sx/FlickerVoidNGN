using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Debug;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;

using Hexa.NET.ImGui;

namespace FlickerVoid.Launcher;

/// <summary>
/// The story while the game interface is still to come: the conversation's line and choices (picked here), the quests with their
/// visible objectives, and what the protagonist knows.
/// </summary>
public sealed class StoryWindow(DialogueSystem dialogue, QuestSystem quests, KnowledgeSystem knowledge, DataAssets data, World world)
    : DebugWindow("Story", "Game")
{
    public override void Render()
    {
        Vector2 size = new(430, 270);
        Vector2 position = new(830, 430);
        ImGui.SetWindowSize(size, ImGuiCond.FirstUseEver);
        ImGui.SetWindowPos(position, ImGuiCond.FirstUseEver);
        ImGui.SeparatorText("Conversation");
        RenderConversation();
        ImGui.SeparatorText("Quests");
        RenderQuests();
        ImGui.SeparatorText("Knowledge");
        RenderKnowledge();
    }

    private void RenderConversation()
    {
        if (!dialogue.IsInConversation)
        {
            ImGui.TextDisabled("Click someone to talk to them.");
            return;
        }

        DialogueLine line = dialogue.CurrentLine;
        if (line.Text.Length > 0)
        {
            string speaker = "Someone";
            if (line.SpeakerEntity != Entity.Null && world.IsAlive(line.SpeakerEntity) && world.TryGet(line.SpeakerEntity, out DialogueParticipant participant))
            {
                string name = participant.Speaker.Name;
                speaker = name[(name.LastIndexOf('.') + 1)..];
            }

            ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.45f, 1), speaker);
            ImGui.TextWrapped(line.Text);
            if (!dialogue.IsWaitingForChoice && ImGui.Button("Next"))
            {
                dialogue.Advance();
            }
        }

        foreach (DialogueChoice choice in dialogue.Choices)
        {
            string label = $"{choice.Index + 1}. {choice.Text}##choice{choice.Index}";
            ImGui.BeginDisabled(!choice.Available);
            if (ImGui.Button(label))
            {
                dialogue.Choose(choice.Index);
            }

            ImGui.EndDisabled();
            if (choice.LockedReason.Length > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(choice.LockedReason);
            }
        }

        if (ImGui.Button("Leave"))
        {
            dialogue.End();
        }
    }

    private void RenderQuests()
    {
        IReadOnlyList<QuestDefinition> all = data.LoadAll<QuestDefinition>();
        foreach (QuestDefinition quest in all)
        {
            QuestState state = quests.GetState(quest.Id);
            if (state == QuestState.NotStarted)
            {
                continue;
            }

            ImGui.Text($"{quest.Display.Name}: {state}");
            foreach (QuestObjective objective in quest.Objectives)
            {
                if (!quests.IsObjectiveVisible(objective))
                {
                    continue;
                }

                bool done = quests.IsObjectiveComplete(objective.Fact);
                string mark = done ? "[x]" : "[ ]";
                ImGui.BulletText($"{mark} {objective.Description}");
            }
        }
    }

    private void RenderKnowledge()
    {
        foreach (KnowledgeDefinition known in knowledge.GetKnown())
        {
            ImGui.BulletText($"{known.Kind}: {known.Display.Name}");
        }
    }
}
