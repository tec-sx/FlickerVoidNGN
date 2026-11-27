namespace FlickerVoid.Engine.Foliage;

/// <summary>The numbers a tree or bush is grown from, in metres; with the file's seed they give the same plant every time.</summary>
public sealed class FoliageShape
{
    /// <summary>How tall the plant stands.</summary>
    public float Height { get; set; } = 6;

    /// <summary>The trunk's radius at its foot; a bush has none.</summary>
    public float TrunkRadius { get; set; } = 0.25f;

    /// <summary>How many limbs leave the trunk.</summary>
    public int Limbs { get; set; } = 4;

    /// <summary>How far the crown reaches from the trunk's top.</summary>
    public float CrownRadius { get; set; } = 1.8f;

    /// <summary>How many rounded masses of leaves the crown is made of.</summary>
    public int Crowns { get; set; } = 5;

    /// <summary>The shape a new plant of the kind is grown from.</summary>
    public static FoliageShape Create(FoliageKind kind)
    {
        if (kind == FoliageKind.Bush)
        {
            return new FoliageShape { Height = 1, TrunkRadius = 0, Limbs = 0, CrownRadius = 0.7f, Crowns = 3 };
        }

        return new FoliageShape();
    }

    /// <summary>Whether a plant can be grown from these numbers.</summary>
    public bool IsValid()
    {
        bool size = Height is >= 0.2f and <= 40 && CrownRadius is >= 0.1f and <= 15 && TrunkRadius is >= 0 and <= 3;
        return size && Limbs is >= 0 and <= 12 && Crowns is >= 1 and <= 16;
    }

    public FoliageShape Clone() => (FoliageShape)MemberwiseClone();
}
