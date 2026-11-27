using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Foliage;

/// <summary>
/// Plants scattered over the ground around the entity: copies of one foliage file at places drawn from the seed within the radius,
/// each turned and sized by chance. The copies stand at the entity's height and stop nobody.
/// </summary>
public struct FoliageScatter
{
    public FoliageScatter()
    {
    }

    [ContentFile(FoliageFile.Extension)]
    public required string File { get; set; }

    /// <summary>How many copies are scattered, up to <see cref="MaxCount"/>.</summary>
    public int Count { get; set; } = 12;

    /// <summary>How far from the entity the copies may stand, in metres.</summary>
    public float Radius { get; set; } = 4;

    /// <summary>The number the places are drawn from: another seed scatters them another way.</summary>
    public int Seed { get; set; } = 1;

    /// <summary>The smallest a copy is, as a share of the file's size.</summary>
    public float MinScale { get; set; } = 0.8f;

    /// <summary>The largest a copy is, as a share of the file's size.</summary>
    public float MaxScale { get; set; } = 1.2f;

    /// <summary>The most copies one scatter makes.</summary>
    public const int MaxCount = 200;
}
