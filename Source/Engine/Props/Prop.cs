using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Props;

/// <summary>A prop placed in a level: the file it is made from, whose parts are spawned under the entity.</summary>
public struct Prop
{
    [ContentFile(PropFile.Extension)]
    public required string File { get; set; }
}
