using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Localization;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>
/// The player's choice among at most three options, one output each; options whose conditions fail are hidden or shown locked.
/// With no option available the conversation ends; ending it is also the player's way to leave.
/// </summary>
public sealed class ChoiceNode : FlowNode<ChoiceNode.Out>
{
    public const int MaxOptions = 3;

    public enum Out
    {
        First,
        Second,
        Third,
    }

    /// <summary>Up to <see cref="MaxOptions"/>; more are ignored.</summary>
    public List<DialogueChoiceOption> Options { get; set; } = [];

    public override string Describe() => $"{Options.Count} options";

    protected override void Execute(FlowContext<Out> context)
    {
        DialogueSystem? dialogue = context.Conditions.GetService<DialogueSystem>();
        if (dialogue is null)
        {
            return;
        }

        dialogue.EnsureConversation(context.Run);
        List<DialogueChoice> choices = [];
        bool anyAvailable = false;
        int count = Math.Min(Options.Count, MaxOptions);
        for (int index = 0; index < count; index++)
        {
            ConditionSet conditions = Options[index].Conditions;
            bool available = conditions.Evaluate(context.Conditions);
            anyAvailable |= available;
            if (!available && conditions.FailurePresentation == ConditionFailurePresentation.Hidden)
            {
                continue;
            }

            LocalizedText reason = new();
            if (!available && conditions.FailurePresentation == ConditionFailurePresentation.ShowLockedWithReason)
            {
                reason = conditions.FailureReason;
                if (reason.IsEmpty)
                {
                    reason = new LocalizedText("", conditions.Describe());
                }
            }

            DialogueChoice choice = new(index, Options[index].Text, available, reason);
            choices.Add(choice);
        }

        if (!anyAvailable)
        {
            dialogue.End();
            return;
        }

        context.StayActive();
        dialogue.PresentChoices(choices, context.Run, this, index => Choose(context, index));
    }

    protected override void Cleanup(FlowContext<Out> context)
    {
        DialogueSystem? dialogue = context.Conditions.GetService<DialogueSystem>();
        dialogue?.Cancel(context.Run, this);
    }

    private void Choose(FlowContext<Out> context, int index)
    {
        Options[index].OnChosen.Apply(context.Conditions);
        context.Trigger((Out)index);
    }
}
