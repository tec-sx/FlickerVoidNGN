using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>Makes the body on wheels a vehicle file drives as: a hull of its body's points, and a wheel where each wheel part stands.</summary>
internal static class VehicleWheels
{
    public static WheeledBody CreateBody(VehicleFile file)
    {
        VehicleDrive drive = file.GetDrive();
        Matrix4x4 toVehicle = file.Body.Place.ToMatrix();
        Vector3[] points = [.. file.Body.Mesh.Points.Select(point => Vector3.Transform(point, toVehicle))];
        HullCollider chassis = new() { Points = points };
        float front = file.Wheels[0].Place.Position.Z;
        BodyWheel[] wheels = new BodyWheel[file.Wheels.Count];
        for (int index = 0; index < wheels.Length; index++)
        {
            SolidPart part = file.Wheels[index];
            bool first = part.Place.Position.Z == front;
            wheels[index] = CreateWheel(part, drive, first);
        }

        return new WheeledBody
        {
            Collider = chassis,
            Mass = drive.Mass,
            Wheels = wheels,
            EngineTorque = drive.EngineTorque,
            BrakeTorque = drive.BrakeTorque,
            SuspensionTravel = drive.SuspensionTravel,
            SuspensionFrequency = drive.SuspensionFrequency,
            SuspensionDamping = drive.SuspensionDamping,
            Leans = file.Leans,
        };
    }

    /// <summary>A wheel as round and as wide as the part's mesh about its pivot; the first axle steers, and the rest hold the hand brake.</summary>
    private static BodyWheel CreateWheel(SolidPart part, VehicleDrive drive, bool first)
    {
        Matrix4x4 toVehicle = part.Place.ToMatrix();
        float radius = 0;
        float left = 0;
        float right = 0;
        foreach (Vector3 point in part.Mesh.Points)
        {
            Vector3 turned = Vector3.TransformNormal(point, toVehicle);
            float distance = MathF.Sqrt((turned.Y * turned.Y) + (turned.Z * turned.Z));
            radius = MathF.Max(radius, distance);
            left = MathF.Max(left, turned.X);
            right = MathF.Min(right, turned.X);
        }

        bool driven = drive.Driven == DrivenAxles.All || (drive.Driven == DrivenAxles.Front) == first;
        BodyWheel wheel = new()
        {
            Position = part.Place.Position,
            Radius = radius,
            Width = left - right,
            Driven = driven,
            HandBrake = !first,
        };
        if (first)
        {
            wheel.MaxSteer = drive.Steer;
        }

        return wheel;
    }
}
