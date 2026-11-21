using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Checks;

/// <summary>A skill check: the instigator's attribute, a roll and conditional bonuses, against a difficulty its user gives.</summary>
public sealed class CheckDefinition : Definition
{
    public Tag Attribute { get; set; }

    public CheckRoll Roll { get; set; } = CheckRoll.D6x2;

    public List<CheckModifier> Modifiers { get; set; } = [];
}
