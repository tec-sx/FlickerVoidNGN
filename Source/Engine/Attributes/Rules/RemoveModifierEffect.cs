using Arch.Core;

using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Attributes.Rules;

/// <summary>Takes every instance of a modifier off the subject, the instigator by default, such as a status that ends with a quest step.</summary>
public sealed class RemoveModifierEffect : Effect
{
    [ContentFile(".fvdata")]
    public string Modifier { get; set; } = string.Empty;

    public ContextEntity Subject { get; set; }

    public override string Describe()
    {
        string name = AttributeContext.FileName(Modifier);
        return $"Remove {name}";
    }

    public override void Apply(ConditionContext context)
    {
        ModifierDefinition? modifier = AttributeContext.LoadModifier(context, Modifier);
        if (modifier is not null && AttributeContext.TryGetOwner(context, Subject, out AttributeSystem attributes, out Entity owner))
        {
            attributes.RemoveModifiers(owner, modifier);
        }
    }
}
