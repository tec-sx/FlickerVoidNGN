using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Foliage;

/// <summary>A tree or bush placed in a level: the file it is grown from, whose parts are spawned under the entity.</summary>
public struct Plant
{
    [ContentFile(FoliageFile.Extension)]
    public required string File { get; set; }
}
