using Arch.Core;

using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Rules;

/// <summary>Permanently sets the base value of the subject's attribute, the instigator's by default.</summary>
public sealed class SetAttributeEffect : Effect
{
    public Tag Attribute { get; set; }

    public float Value { get; set; }

    public ContextEntity Subject { get; set; }

    public override string Describe()
    {
        string name = AttributeContext.Name(Attribute);
        return $"{name} = {Value}";
    }

    public override void Apply(ConditionContext context)
    {
        if (AttributeContext.TryGetOwner(context, Subject, out AttributeSystem attributes, out Entity owner))
        {
            attributes.SetBaseValue(owner, Attribute, Value);
        }
    }
}
