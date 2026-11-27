using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Foliage;

/// <summary>
/// A <c>Name.fvfoliage</c> file: a tree or a bush grown from a <see cref="Seed"/> and a <see cref="Shape"/>, kept as the parts
/// growing made, so the mesh tools can shape them afterwards. Part ids: <c>trunk</c>, <c>limb.1</c>, <c>crown.1</c>. Growing again
/// replaces every part.
/// </summary>
public sealed class FoliageFile : ICreationFile
{
    public const string Extension = ".fvfoliage";

    public const string TrunkId = "trunk";

    private const int TrunkSides = 6;
    private const int LimbSides = 5;

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    public FoliageKind Kind { get; set; }

    /// <summary>The number the plant's chances are drawn from: another seed grows another plant of the same shape.</summary>
    public int Seed { get; set; } = 1;

    public FoliageShape Shape { get; set; } = new();

    /// <summary>The trunk, standing on the plant's origin; a bush has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidPart? Trunk { get; set; }

    /// <summary>The limbs, numbered from 1 in order: <c>limb.1</c>.</summary>
    public List<SolidPart> Limbs { get; set; } = [];

    /// <summary>The crown's masses of leaves, numbered from 1 in order: <c>crown.1</c>.</summary>
    public List<SolidPart> Crowns { get; set; } = [];

    /// <summary>Foliage has no doors; the list is here for the creators' shared tools and is never written.</summary>
    [JsonIgnore]
    public List<SolidPart> Doors { get; } = [];

    /// <summary>A new plant of the kind, grown from the kind's shape and the seed.</summary>
    public static FoliageFile Create(FoliageKind kind, int seed = 1)
    {
        FoliageFile foliage = new() { Kind = kind, Seed = seed, Shape = FoliageShape.Create(kind) };
        foliage.Grow();
        return foliage;
    }

    /// <summary>Makes it a tree or a bush, with that kind's shape, and grows it.</summary>
    public void SetKind(FoliageKind kind)
    {
        Kind = kind;
        Shape = FoliageShape.Create(kind);
        Grow();
    }

    /// <summary>
    /// Grows the plant anew from its kind, seed and shape, replacing every part and what the mesh tools made of them; throws for a
    /// shape nothing grows from.
    /// </summary>
    public void Grow()
    {
        if (!Shape.IsValid())
        {
            throw new ArgumentException("A plant is 0.2 to 40 metres tall, with a crown of 0.1 to 15 metres, a trunk up to 3 metres wide, up to 12 limbs and 1 to 16 crowns.");
        }

        Random random = new(Seed);
        Trunk = null;
        Limbs.Clear();
        Crowns.Clear();
        if (Kind == FoliageKind.Bush || Shape.TrunkRadius <= 0)
        {
            GrowBush(random);
        }
        else
        {
            GrowTree(random);
        }
    }

    /// <summary>Every part with its id: the trunk, the limbs, then the crowns.</summary>
    public IReadOnlyList<(string Id, SolidPart Part)> GetParts()
    {
        List<(string Id, SolidPart Part)> parts = [];
        if (Trunk is { } trunk)
        {
            parts.Add((TrunkId, trunk));
        }

        for (int index = 0; index < Limbs.Count; index++)
        {
            parts.Add(($"limb.{index + 1}", Limbs[index]));
        }

        for (int index = 0; index < Crowns.Count; index++)
        {
            parts.Add(($"crown.{index + 1}", Crowns[index]));
        }

        return parts;
    }

    private void GrowTree(Random random)
    {
        float trunkHeight = MathF.Max(Shape.Height - Shape.CrownRadius, Shape.Height * 0.3f);
        SolidMesh trunk = FoliageShapes.CreateTaper(Shape.TrunkRadius, Shape.TrunkRadius / 2, trunkHeight, TrunkSides);
        Trunk = new SolidPart { Mesh = trunk };
        List<Vector3> tips = [];
        for (int index = 0; index < Shape.Limbs; index++)
        {
            float along = 0.45f + (0.4f * random.NextSingle());
            float yaw = (index + (0.5f * random.NextSingle())) * MathF.Tau / Shape.Limbs;
            float lean = (50 + (20 * random.NextSingle())) * MathF.PI / 180;
            float length = Shape.CrownRadius * (0.7f + (0.5f * random.NextSingle()));
            Vector3 direction = new(MathF.Sin(lean) * MathF.Cos(yaw), MathF.Cos(lean), MathF.Sin(lean) * MathF.Sin(yaw));
            Vector3 axis = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction));
            Vector3 foot = new(0, along * trunkHeight, 0);
            float radius = Shape.TrunkRadius * 0.35f;
            SolidMesh limb = FoliageShapes.CreateTaper(radius, radius / 2, length, LimbSides);
            Transform place = new() { Position = foot, Rotation = Quaternion.CreateFromAxisAngle(axis, lean) };
            Limbs.Add(new SolidPart { Place = place, Mesh = limb });
            tips.Add(foot + (direction * length));
        }

        for (int index = 0; index < Shape.Crowns; index++)
        {
            Vector3 centre = new(0, trunkHeight, 0);
            float share = 0.8f + (0.2f * random.NextSingle());
            if (index > 0)
            {
                share = 0.5f + (0.25f * random.NextSingle());
                centre = FindCrownPlace(random, tips, index, trunkHeight);
            }

            float across = Shape.CrownRadius * share;
            Vector3 radii = new(across, across * 0.8f, across * (0.85f + (0.3f * random.NextSingle())));
            AddCrown(centre, radii);
        }
    }

    /// <summary>The later crowns sit on the limbs' tips in turn, or around the top when the tree has no limbs.</summary>
    private Vector3 FindCrownPlace(Random random, List<Vector3> tips, int index, float trunkHeight)
    {
        if (tips.Count > 0)
        {
            return tips[(index - 1) % tips.Count];
        }

        float angle = MathF.Tau * random.NextSingle();
        float reach = Shape.CrownRadius * 0.6f * random.NextSingle();
        return new Vector3(MathF.Cos(angle) * reach, trunkHeight - (0.3f * Shape.CrownRadius * random.NextSingle()), MathF.Sin(angle) * reach);
    }

    private void GrowBush(Random random)
    {
        for (int index = 0; index < Shape.Crowns; index++)
        {
            float angle = (index + random.NextSingle()) * MathF.Tau / Shape.Crowns;
            float reach = 0;
            if (index > 0)
            {
                reach = Shape.CrownRadius * (0.3f + (0.3f * random.NextSingle()));
            }

            float across = Shape.CrownRadius * (0.5f + (0.4f * random.NextSingle()));
            float half = Shape.Height / 2 * (0.7f + (0.3f * random.NextSingle()));
            if (index == 0)
            {
                half = Shape.Height / 2;
            }

            Vector3 centre = new(MathF.Cos(angle) * reach, half * 0.9f, MathF.Sin(angle) * reach);
            AddCrown(centre, new Vector3(across, half, across));
        }
    }

    private void AddCrown(Vector3 centre, Vector3 radii)
    {
        SolidMesh blob = FoliageShapes.CreateBlob(radii);
        Transform place = new() { Position = centre };
        Crowns.Add(new SolidPart { Place = place, Mesh = blob });
    }
}
