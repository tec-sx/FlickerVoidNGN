using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Engine.Props;

/// <summary>One named part of a prop: its id in the file and in the level, and its solid.</summary>
public sealed class PropPart
{
    public required string Name { get; set; }

    public required SolidPart Part { get; set; }
}
