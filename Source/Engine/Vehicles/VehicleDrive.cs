namespace FlickerVoid.Engine.Vehicles;

/// <summary>How a vehicle drives: its weight, engine, brakes, steering and suspension. A file without one drives with these values.</summary>
public sealed class VehicleDrive
{
    /// <summary>The vehicle's weight, in kilograms.</summary>
    public float Mass { get; set; } = 1500;

    /// <summary>The engine's greatest torque, in newton metres.</summary>
    public float EngineTorque { get; set; } = 500;

    /// <summary>The brakes' greatest torque on each wheel, in newton metres.</summary>
    public float BrakeTorque { get; set; } = 1500;

    /// <summary>Which axles the engine turns.</summary>
    public DrivenAxles Driven { get; set; } = DrivenAxles.Rear;

    /// <summary>How far the front wheels turn at full steering, in degrees.</summary>
    public float Steer { get; set; } = 35;

    /// <summary>How far each wheel travels on its suspension, in metres.</summary>
    public float SuspensionTravel { get; set; } = 0.3f;

    /// <summary>How fast the suspension springs back, in cycles a second; softer is lower.</summary>
    public float SuspensionFrequency { get; set; } = 1.5f;

    /// <summary>How quickly the suspension stops bouncing, from 0 (never) to 1 (at once).</summary>
    public float SuspensionDamping { get; set; } = 0.5f;

    /// <summary>The values a new vehicle of the type drives with: a motorcycle is light, with an engine to match.</summary>
    public static VehicleDrive Create(VehicleType type)
    {
        if (type == VehicleType.Motorcycle)
        {
            return new VehicleDrive { Mass = 250, EngineTorque = 150, BrakeTorque = 500, Steer = 30 };
        }

        return new VehicleDrive();
    }
}
