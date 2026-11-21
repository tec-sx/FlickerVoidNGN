using Arch.Core;

using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Dialogue.Barks;

/// <summary>Says a fitting bark of a table, not the one the speaker said last, and passes the signal on, after it shows when it waits.</summary>
public sealed class BarkNode : FlowNode<FlowOutput>
{
    /// <summary>The <c>bark</c> table.</summary>
    [ContentFile(".fvtable")]
    public string Barks { get; set; } = "";

    /// <summary>Which side of the flow speaks; the other side's tags count too.</summary>
    public ContextEntity Speaker { get; set; } = ContextEntity.Target;

    public bool WaitForDuration { get; set; } = true;

    public override string Describe() => Barks;

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        DialogueSystem? dialogue = context.Conditions.GetService<DialogueSystem>();
        DataAssets? data = context.Conditions.GetService<DataAssets>();
        IEntityView? view = context.Conditions.GetEntity(Speaker);
        if (dialogue is null || data is null || view is not WorldEntityView speaker || Barks.Length == 0)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        DataTable<BarkRow> table = data.LoadTable<BarkRow>(Barks);
        KeyValuePair<string, BarkRow>? picked = Pick(table, speaker.Entity, dialogue, context.Conditions);
        if (picked is not { } row)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        float duration = row.Value.DisplayDuration;
        dialogue.Bark(speaker.Entity, row.Key, row.Value.Text, duration);
        if (!WaitForDuration || duration <= 0)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        context.StayActive();
        dialogue.WaitFor(duration, context.Run, this, () => context.Trigger(FlowOutput.Out));
    }

    protected override void Cleanup(FlowContext<FlowOutput> context)
    {
        DialogueSystem? dialogue = context.Conditions.GetService<DialogueSystem>();
        dialogue?.Cancel(context.Run, this);
    }

    private static bool Owns(IReadOnlyList<Tag> owned, Tag tag)
    {
        foreach (Tag ownedTag in owned)
        {
            if (ownedTag.MatchesTag(tag))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<Tag> GetTags(IEntityView? view)
    {
        if (view is not null && view.TryGet(out OwnedTags owned))
        {
            return owned.Tags;
        }

        return [];
    }

    private KeyValuePair<string, BarkRow>? Pick(DataTable<BarkRow> table, Entity speaker, DialogueSystem dialogue, ConditionContext context)
    {
        ContextEntity otherSide = Speaker == ContextEntity.Target ? ContextEntity.Instigator : ContextEntity.Target;
        IReadOnlyList<Tag> speakerTags = GetTags(context.GetEntity(Speaker));
        IReadOnlyList<Tag> otherTags = GetTags(context.GetEntity(otherSide));
        List<Tag> combined = [.. speakerTags, .. otherTags];

        List<KeyValuePair<string, BarkRow>> fitting = [];
        foreach (KeyValuePair<string, BarkRow> entry in table.Rows)
        {
            BarkRow row = entry.Value;
            bool identity = !row.IdentityTag.IsValid || Owns(speakerTags, row.IdentityTag);
            bool required = row.RequiredTags.All(tag => Owns(combined, tag));
            bool blocked = row.BlockingTags.Any(tag => Owns(combined, tag));
            if (identity && required && !blocked && row.Conditions.Evaluate(context))
            {
                fitting.Add(entry);
            }
        }

        string? last = dialogue.GetLastBark(speaker);
        if (last is not null && fitting.Count > 1)
        {
            fitting.RemoveAll(entry => entry.Key == last);
        }

        if (fitting.Count == 0)
        {
            return null;
        }

        int index = dialogue.Random.Next(fitting.Count);
        return fitting[index];
    }
}
