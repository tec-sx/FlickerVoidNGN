namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>How a label over the building creator's viewport looks: a room's name and area, a dimension, or the tag of a drag.</summary>
public enum PlanLabelKind
{
    Room,
    Dimension,
    Drag,
    Shot,
    Trigger,

    /// <summary>The tag of what is cut out, in the cut's red.</summary>
    Cut,
}
