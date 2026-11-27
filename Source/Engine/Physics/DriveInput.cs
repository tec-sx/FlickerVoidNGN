namespace FlickerVoid.Engine.Physics;

/// <summary>What the driver of a <see cref="WheeledBody"/> asks of it this step; game code sets it, and it is never saved.</summary>
public struct DriveInput
{
    /// <summary>The accelerator, from -1 (full reverse) to 1 (full forward).</summary>
    public float Forward;

    /// <summary>The steering, from -1 (full left) to 1 (full right).</summary>
    public float Right;

    /// <summary>The brake pedal, from 0 to 1.</summary>
    public float Brake;

    /// <summary>The hand brake, from 0 to 1.</summary>
    public float HandBrake;
}
