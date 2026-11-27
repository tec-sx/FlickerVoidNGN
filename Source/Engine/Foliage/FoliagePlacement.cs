using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Foliage;

/// <summary>Where scattered foliage stands.</summary>
public static class FoliagePlacement
{
    /// <summary>Where each copy of a scatter stands about its entity, drawn from its seed: the same every time.</summary>
    public static IReadOnlyList<Transform> Scatter(in FoliageScatter scatter)
    {
        int count = Math.Clamp(scatter.Count, 0, FoliageScatter.MaxCount);
        float radius = MathF.Max(scatter.Radius, 0);
        float smallest = MathF.Max(MathF.Min(scatter.MinScale, scatter.MaxScale), 0.05f);
        float largest = MathF.Max(scatter.MaxScale, smallest);
        Random random = new(scatter.Seed);
        Transform[] places = new Transform[count];
        for (int index = 0; index < count; index++)
        {
            // The root of the chance spreads the copies evenly over the disc rather than crowding its middle.
            float reach = radius * MathF.Sqrt(random.NextSingle());
            float angle = MathF.Tau * random.NextSingle();
            float turn = MathF.Tau * random.NextSingle();
            float size = smallest + ((largest - smallest) * random.NextSingle());
            places[index] = new Transform
            {
                Position = new Vector3(MathF.Cos(angle) * reach, 0, MathF.Sin(angle) * reach),
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn),
                Scale = new Vector3(size),
            };
        }

        return places;
    }
}
