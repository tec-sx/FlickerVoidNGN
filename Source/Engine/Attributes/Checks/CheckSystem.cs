using Arch.Core;

using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Attributes.Checks;

/// <summary>Rolls and previews skill checks for the context's instigator, and tells the chance of success.</summary>
public sealed class CheckSystem(AttributeSystem attributes, Random random)
{
    /// <summary>The roll's line of a breakdown; a game translates it by listing its key in a table of texts as written.</summary>
    public static LocalizedText RollLabel { get; } = new("Check.Roll", "Roll", "The dice's part of a skill check's total.");

    /// <summary>The check with its roll: the attribute, every bonus whose conditions pass and the dice.</summary>
    public CheckResult Roll(CheckDefinition check, ConditionContext context, int difficulty) => Build(check, context, difficulty, roll: true);

    /// <summary>The check without rolling, as a passive check or the interface's preview.</summary>
    public CheckResult Preview(CheckDefinition check, ConditionContext context, int difficulty) => Build(check, context, difficulty, roll: false);

    /// <summary>The chance, from 0 to 1, that rolling the check meets the difficulty.</summary>
    public float SuccessChance(CheckDefinition check, ConditionContext context, int difficulty)
    {
        CheckResult passive = Preview(check, context, difficulty);
        return ChanceAtLeast(check.Roll, difficulty - passive.Total);
    }

    /// <summary>The chance that the roll comes up <paramref name="needed"/> or higher; 2d6 counts its 36 outcomes.</summary>
    public static float ChanceAtLeast(CheckRoll roll, int needed)
    {
        int faces;
        switch (roll)
        {
            case CheckRoll.D6x2:
                int hits = 0;
                for (int first = 1; first <= 6; first++)
                {
                    for (int second = 1; second <= 6; second++)
                    {
                        if (first + second >= needed)
                        {
                            hits++;
                        }
                    }
                }

                return hits / 36f;
            case CheckRoll.D10:
                faces = 10;
                break;
            case CheckRoll.D20:
                faces = 20;
                break;
            default:
                if (needed <= 0)
                {
                    return 1;
                }

                return 0;
        }

        int succeeding = Math.Clamp(faces - needed + 1, 0, faces);
        return succeeding / (float)faces;
    }

    private CheckResult Build(CheckDefinition check, ConditionContext context, int difficulty, bool roll)
    {
        List<CheckLine> breakdown = [];
        int total = 0;
        if (context.Instigator is WorldEntityView instigator && attributes.HasAttribute(instigator.Entity, check.Attribute))
        {
            float value = attributes.GetValue(instigator.Entity, check.Attribute);
            int rounded = (int)MathF.Round(value, MidpointRounding.AwayFromZero);
            LocalizedText label = GetLabel(check.Attribute);
            total += rounded;
            breakdown.Add(new CheckLine(label, rounded));
        }

        foreach (CheckModifier modifier in check.Modifiers)
        {
            if (modifier.Conditions.IsEmpty || modifier.Conditions.Evaluate(context))
            {
                total += modifier.Bonus;
                breakdown.Add(new CheckLine(modifier.Label, modifier.Bonus));
            }
        }

        if (roll)
        {
            int rolled = RollDice(check.Roll);
            total += rolled;
            breakdown.Add(new CheckLine(RollLabel, rolled));
        }

        return new CheckResult(total >= difficulty, total, difficulty, breakdown);
    }

    private LocalizedText GetLabel(Tag attribute)
    {
        AttributeDefinition? definition = attributes.FindDefinition(attribute);
        if (definition is not null && !definition.Display.Name.IsEmpty)
        {
            return definition.Display.Name;
        }

        string name = attribute.Name;
        int dot = name.LastIndexOf('.');
        return new LocalizedText("", name[(dot + 1)..]);
    }

    private int RollDice(CheckRoll roll)
    {
        switch (roll)
        {
            case CheckRoll.D6x2:
                int first = random.Next(1, 7);
                int second = random.Next(1, 7);
                return first + second;
            case CheckRoll.D10:
                return random.Next(1, 11);
            case CheckRoll.D20:
                return random.Next(1, 21);
            default:
                return 0;
        }
    }
}
