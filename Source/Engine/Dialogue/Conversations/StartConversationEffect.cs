using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>Starts the target's dialogue with the instigator, as a Talk offer does.</summary>
public sealed class StartConversationEffect : Effect
{
    public override void Apply(ConditionContext context)
    {
        DialogueSystem? dialogue = context.GetService<DialogueSystem>();
        if (dialogue is null || context.Instigator is not WorldEntityView instigator || context.Target is not WorldEntityView owner)
        {
            return;
        }

        dialogue.Start(instigator.Entity, owner.Entity);
    }

    public override string Describe() => "Talk";
}
