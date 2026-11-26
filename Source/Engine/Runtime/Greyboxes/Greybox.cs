using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Greyboxes;

/// <summary>
/// A block-out box standing on its entity, drawn grey until one of its swap models is shown in its place, so a level can be
/// laid out and played before its models are made and each model can be tried where it will stand.
/// </summary>
public struct Greybox
{
    public Greybox()
    {
    }

    /// <summary>Width, height and length; the box stands on the entity's position, centred across.</summary>
    public Vector3 Size { get; set; } = Vector3.One;

    /// <summary>Whether others collide with it: while playing, physics gives it a static box of its size.</summary>
    public bool Solid { get; set; } = true;

    /// <summary>The material the box is drawn in, such as glass or neon; none draws it grey. A swap model keeps its own.</summary>
    [ContentFile(".fvmat")]
    public string? Material { get; set; }

    /// <summary>Models that may stand in the box's place, drawn at the entity's transform.</summary>
    [ContentFile(".glb", ".gltf")]
    public string[]? Swaps { get; set; }

    /// <summary>The swap shown, counted from 1; 0 shows the box.</summary>
    public int Swap { get; set; }

    /// <summary>The model shown in the box's place, or null when the box is shown.</summary>
    public readonly string? GetShownSwap()
    {
        if (Swaps is null || Swap < 1 || Swap > Swaps.Length)
        {
            return null;
        }

        string mesh = Swaps[Swap - 1];
        if (mesh.Length == 0)
        {
            return null;
        }

        return mesh;
    }
}
