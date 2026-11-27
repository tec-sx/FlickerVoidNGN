using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// A <c>Name.fvvehicle</c> file: what the vehicle is, its body and its wheels, front axle first and left before right. Part ids
/// follow from the order: <c>body</c>, then <c>wheel.1L</c> and <c>wheel.1R</c> for the first axle, or <c>wheel.1</c> where an
/// axle has one wheel. A file whose wheel count its type doesn't take, or whose meshes aren't closed, can't be read.
/// </summary>
public sealed class VehicleFile : IJsonOnDeserialized
{
    public const string Extension = ".fvvehicle";

    public const string BodyId = "body";

    private static readonly int[] CarWheels = [4, 6, 8];
    private static readonly int[] MotorcycleWheels = [2, 4];

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>What the vehicle is; <see cref="SetType"/> changes it and fits the wheels to it.</summary>
    public VehicleType Type { get; set; }

    public required VehiclePart Body { get; set; }

    /// <summary>The wheels, front axle first and left before right; <see cref="SetWheelCount"/> changes how many.</summary>
    public List<VehiclePart> Wheels { get; set; } = [];

    /// <summary>The doors, numbered from 1 in order: <c>door.1</c>, <c>door.2</c>; the door tool adds them.</summary>
    public List<VehiclePart> Doors { get; set; } = [];

    /// <summary>How the vehicle drives; a file without it drives as a new vehicle of its type does (<see cref="GetDrive"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VehicleDrive? Drive { get; set; }

    /// <summary>Whether the vehicle balances on wheels in line and leans into turns: a motorcycle on two.</summary>
    [JsonIgnore]
    public bool Leans => Type == VehicleType.Motorcycle && Wheels.Count == 2;

    /// <summary>How the vehicle drives: its own values, else those a new vehicle of its type starts with.</summary>
    public VehicleDrive GetDrive() => Drive ?? VehicleDrive.Create(Type);

    /// <summary>A new vehicle of the type: a box body and the fewest wheels it takes.</summary>
    public static VehicleFile Create(VehicleType type)
    {
        VehiclePart body = VehicleShapes.CreateBody(type);
        VehicleFile vehicle = new() { Type = type, Body = body };
        int count = GetWheelCounts(type)[0];
        int perAxle = GetWheelsPerAxle(type, count);
        List<VehiclePart> wheels = VehicleShapes.CreateWheels(type, perAxle);
        vehicle.Wheels.AddRange(wheels);
        return vehicle;
    }

    /// <summary>The wheel counts the type takes, fewest first, until driving settles the full rules.</summary>
    public static IReadOnlyList<int> GetWheelCounts(VehicleType type)
    {
        switch (type)
        {
            case VehicleType.Motorcycle:
                return MotorcycleWheels;
            default:
                return CarWheels;
        }
    }

    /// <summary>
    /// Adds axles behind the last or removes the last ones until the vehicle has the count. The body's rear follows the last axle,
    /// longer or shorter; its front and every other wheel stay as they were.
    /// </summary>
    public void SetWheelCount(int count)
    {
        IReadOnlyList<int> counts = GetWheelCounts(Type);
        if (!counts.Contains(count))
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, Describe(Type, counts));
        }

        Arrange(Type, count);
    }

    /// <summary>
    /// Makes the vehicle the type, with the wheel count nearest the one it has that the type takes. The wheels stay while they sit
    /// the same way, one or two to an axle, and the body's rear follows the last axle; otherwise the wheels are made new and the body
    /// stays as it is.
    /// </summary>
    public void SetType(VehicleType type)
    {
        IReadOnlyList<int> counts = GetWheelCounts(type);
        int count = counts[0];
        foreach (int candidate in counts)
        {
            if (Math.Abs(candidate - Wheels.Count) < Math.Abs(count - Wheels.Count))
            {
                count = candidate;
            }
        }

        Arrange(type, count);
        Type = type;
    }

    /// <summary>Every part with its id: the body first, then the wheels in order, then the doors.</summary>
    public IReadOnlyList<(string Id, VehiclePart Part)> GetParts()
    {
        List<(string Id, VehiclePart Part)> parts = [(BodyId, Body)];
        int perAxle = GetWheelsPerAxle(Type, Wheels.Count);
        for (int index = 0; index < Wheels.Count; index++)
        {
            int axle = (index / perAxle) + 1;
            string id = $"wheel.{axle}";
            if (perAxle == 2)
            {
                string side = index % 2 == 0 ? "L" : "R";
                id += side;
            }

            parts.Add((id, Wheels[index]));
        }

        for (int index = 0; index < Doors.Count; index++)
        {
            parts.Add(($"door.{index + 1}", Doors[index]));
        }

        return parts;
    }

    void IJsonOnDeserialized.OnDeserialized()
    {
        IReadOnlyList<int> counts = GetWheelCounts(Type);
        if (!counts.Contains(Wheels.Count))
        {
            throw new JsonException($"{Describe(Type, counts)}, not {Wheels.Count}.");
        }
    }

    /// <summary>A motorcycle on two wheels has them in line; every other vehicle has two to an axle.</summary>
    private static int GetWheelsPerAxle(VehicleType type, int count)
    {
        if (type == VehicleType.Motorcycle && count == 2)
        {
            return 1;
        }

        return 2;
    }

    private static string Describe(VehicleType type, IReadOnlyList<int> counts)
    {
        string name = "A car";
        if (type == VehicleType.Motorcycle)
        {
            name = "A motorcycle";
        }

        IEnumerable<int> allButLast = counts.Take(counts.Count - 1);
        string listed = string.Join(", ", allButLast);
        return $"{name} takes {listed} or {counts[^1]} wheels";
    }

    private void Arrange(VehicleType type, int count)
    {
        int had = GetWheelsPerAxle(Type, Wheels.Count);
        int perAxle = GetWheelsPerAxle(type, count);
        if (had != perAxle)
        {
            List<VehiclePart> remade = VehicleShapes.CreateWheels(type, perAxle);
            Wheels.Clear();
            Wheels.AddRange(remade);
        }

        float lastAxle = Wheels[^1].Place.Position.Z;
        while (Wheels.Count > count)
        {
            Wheels.RemoveRange(Wheels.Count - perAxle, perAxle);
        }

        float spacing = VehicleShapes.GetAxleSpacing(type);
        while (Wheels.Count < count)
        {
            List<VehiclePart> last = Wheels.GetRange(Wheels.Count - perAxle, perAxle);
            foreach (VehiclePart wheel in last)
            {
                Transform place = wheel.Place;
                place.Position -= new Vector3(0, 0, spacing);
                VehiclePart added = new() { Place = place, Mesh = wheel.Mesh };
                Wheels.Add(added);
            }
        }

        float moved = Wheels[^1].Place.Position.Z - lastAxle;
        if (moved != 0)
        {
            MoveRear(lastAxle, moved);
        }
    }

    /// <summary>
    /// Moves what the body has behind <paramref name="lastAxle"/>, along the vehicle, by <paramref name="moved"/>: its rear follows
    /// the last axle, so it reaches as far behind the wheels as before.
    /// </summary>
    private void MoveRear(float lastAxle, float moved)
    {
        Matrix4x4 toVehicle = Body.Place.ToMatrix();
        if (!Matrix4x4.Invert(toVehicle, out Matrix4x4 toBody))
        {
            return;
        }

        IReadOnlyList<Vector3> points = Body.Mesh.Points;
        Vector3[] stretched = new Vector3[points.Count];
        for (int index = 0; index < stretched.Length; index++)
        {
            Vector3 point = Vector3.Transform(points[index], toVehicle);
            if (point.Z < lastAxle)
            {
                point.Z += moved;
                stretched[index] = Vector3.Transform(point, toBody);
            }
            else
            {
                stretched[index] = points[index];
            }
        }

        Body.Mesh = new SolidMesh(stretched, Body.Mesh.Faces);
    }
}
