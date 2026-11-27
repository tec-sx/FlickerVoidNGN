using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// The greybox a new vehicle starts as: a box body above two axles of cylinder wheels, sized for its type. Vehicles face +Z, so the
/// first axle is the one furthest along it, and left is +X.
/// </summary>
internal static class VehicleShapes
{
    private const int WheelSides = 16;

    public static VehiclePart CreateBody(VehicleType type)
    {
        Sizes sizes = GetSizes(type);
        SolidMesh mesh = SolidMesh.Box(sizes.Body);
        Transform place = new() { Position = new Vector3(0, sizes.WheelRadius, 0) };
        return new VehiclePart { Place = place, Mesh = mesh };
    }

    /// <summary>Two axles of wheels, one centred or two to an axle, front first and left before right.</summary>
    public static List<VehiclePart> CreateWheels(VehicleType type, int perAxle)
    {
        Sizes sizes = GetSizes(type);
        SolidMesh mesh = CreateWheel(sizes);
        List<VehiclePart> wheels = [];
        float[] axles = [sizes.FrontAxle, sizes.RearAxle];
        foreach (float z in axles)
        {
            if (perAxle == 1)
            {
                wheels.Add(CreateWheelPart(mesh, 0, sizes.WheelRadius, z));
                continue;
            }

            wheels.Add(CreateWheelPart(mesh, sizes.WheelFromCentre, sizes.WheelRadius, z));
            wheels.Add(CreateWheelPart(mesh, -sizes.WheelFromCentre, sizes.WheelRadius, z));
        }

        return wheels;
    }

    /// <summary>How far behind the last axle a new one goes.</summary>
    public static float GetAxleSpacing(VehicleType type)
    {
        Sizes sizes = GetSizes(type);
        return (sizes.WheelRadius * 2) + 0.3f;
    }

    /// <summary>A cylinder centred on its pivot with its axis across the vehicle, so it turns about the pivot.</summary>
    private static SolidMesh CreateWheel(Sizes sizes)
    {
        SolidMesh cylinder = SolidMesh.Cylinder(sizes.WheelRadius, sizes.WheelWidth, WheelSides);
        Matrix4x4 centre = Matrix4x4.CreateTranslation(0, -sizes.WheelWidth / 2, 0);
        Matrix4x4 across = Matrix4x4.CreateRotationZ(-MathF.PI / 2);
        return cylinder.Transform(centre * across);
    }

    private static VehiclePart CreateWheelPart(SolidMesh mesh, float x, float y, float z)
    {
        Transform place = new() { Position = new Vector3(x, y, z) };
        return new VehiclePart { Place = place, Mesh = mesh };
    }

    private static Sizes GetSizes(VehicleType type)
    {
        switch (type)
        {
            case VehicleType.Motorcycle:
                return new Sizes(new Vector3(0.4f, 0.7f, 1.8f), WheelRadius: 0.32f, WheelWidth: 0.12f, WheelFromCentre: 0.45f, FrontAxle: 0.7f, RearAxle: -0.7f);
            case VehicleType.Trailer:
                // Its first axle sits just behind its middle, so it leans on its hitch, and a second follows close behind.
                return new Sizes(new Vector3(1.6f, 0.5f, 2.6f), WheelRadius: 0.3f, WheelWidth: 0.2f, WheelFromCentre: 0.9f, FrontAxle: -0.2f, RearAxle: -1.1f);
            default:
                return new Sizes(new Vector3(1.8f, 1.3f, 4.4f), WheelRadius: 0.35f, WheelWidth: 0.25f, WheelFromCentre: 0.8f, FrontAxle: 1.35f, RearAxle: -1.35f);
        }
    }

    private readonly record struct Sizes(Vector3 Body, float WheelRadius, float WheelWidth, float WheelFromCentre, float FrontAxle, float RearAxle);
}
