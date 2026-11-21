using FlickerVoid.Engine.Attributes.Rules;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Attributes.Checks;

/// <summary>Passes when the check would succeed for the instigator without rolling.</summary>
public sealed class PassiveCheckCondition : Condition
{
    [ContentFile(".fvdata")]
    public string Definition { get; set; } = string.Empty;

    public int Difficulty { get; set; } = 10;

    public override string Describe()
    {
        string name = AttributeContext.FileName(Definition);
        return $"{name} check ({Difficulty})";
    }

    protected override bool Evaluate(ConditionContext context)
    {
        CheckDefinition? check = AttributeContext.Load<CheckDefinition>(context, Definition);
        CheckSystem? checks = context.GetService<CheckSystem>();
        if (check is null || checks is null)
        {
            return false;
        }

        CheckResult result = checks.Preview(check, context, Difficulty);
        return result.Success;
    }
}
