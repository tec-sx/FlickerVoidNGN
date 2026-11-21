using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;

namespace FlickerVoid.Engine.Interaction.Locks;

/// <summary>The subject, the interactable by default, has a lock that is locked.</summary>
public sealed class LockedCondition : Condition
{
    public ContextEntity Subject { get; set; } = ContextEntity.Target;

    public override string Describe() => "Locked";

    protected override bool Evaluate(ConditionContext context)
    {
        IEntityView? subject = context.GetEntity(Subject);
        return subject is not null && subject.TryGet(out Lockable subjectLock) && subjectLock.Locked;
    }
}
