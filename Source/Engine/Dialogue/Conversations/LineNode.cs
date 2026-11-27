using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>A spoken line; it passes the signal on when the line finishes after its duration or the player's input.</summary>
public sealed class LineNode : FlowNode<FlowOutput>
{
    /// <summary>The speaker's character id; none means the conversation's owner.</summary>
    public Tag Speaker { get; set; }

    public LocalizedText Text { get; set; } = new();

    public Tag Mood { get; set; }

    /// <summary>Seconds the line shows; 0 takes it from the text's length.</summary>
    public float Duration { get; set; }

    public bool Skippable { get; set; } = true;

    public bool WaitForInput { get; set; }

    public override string Describe()
    {
        string speaker = Speaker.IsValid ? Speaker.Name : "Owner";
        return $"{speaker}: {Text}";
    }

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        DialogueSystem? dialogue = context.Conditions.GetService<DialogueSystem>();
        if (dialogue is null)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        dialogue.EnsureConversation(context.Run);
        DialogueLine line = new()
        {
            Speaker = Speaker,
            Text = Text,
            Mood = Mood,
            Duration = Duration,
            Skippable = Skippable,
            WaitForInput = WaitForInput,
        };
        context.StayActive();
        dialogue.PlayLine(line, context.Run, this, () => context.Trigger(FlowOutput.Out));
    }

    protected override void Cleanup(FlowContext<FlowOutput> context)
    {
        DialogueSystem? dialogue = context.Conditions.GetService<DialogueSystem>();
        dialogue?.Cancel(context.Run, this);
    }
}
