using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// A <see cref="Quaternion"/> property edited as angles in degrees about X (pitch), Y (yaw) and Z (roll), in the order
/// <see cref="Quaternion.CreateFromYawPitchRoll"/> applies them.
/// </summary>
public sealed partial class RotationFieldViewModel(IEditableValue property) : FieldViewModel(property)
{
    private const float SameRotation = 0.999999f;

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    public partial double Z { get; set; }

    /// <summary>Keeps the angles being typed when they already make the world's rotation, as other angles can make it too.</summary>
    protected override void Show(object? value)
    {
        Quaternion rotation = (Quaternion)value!;
        Quaternion shown = ToQuaternion();
        float alignment = MathF.Abs(Quaternion.Dot(rotation, shown));
        if (alignment >= SameRotation)
        {
            return;
        }

        Vector3 angles = ToAngles(rotation);
        X = angles.X;
        Y = angles.Y;
        Z = angles.Z;
    }

    partial void OnXChanged(double value) => WriteRotation();

    partial void OnYChanged(double value) => WriteRotation();

    partial void OnZChanged(double value) => WriteRotation();

    /// <summary>The pitch, yaw and roll in degrees that <see cref="Quaternion.CreateFromYawPitchRoll"/> turns into the rotation.</summary>
    private static Vector3 ToAngles(Quaternion rotation)
    {
        float sinPitch = 2 * ((rotation.W * rotation.X) - (rotation.Y * rotation.Z));
        float clampedSinPitch = Math.Clamp(sinPitch, -1, 1);
        float pitch = MathF.Asin(clampedSinPitch);
        float yawY = 2 * ((rotation.W * rotation.Y) + (rotation.X * rotation.Z));
        float yawX = 1 - (2 * ((rotation.X * rotation.X) + (rotation.Y * rotation.Y)));
        float yaw = MathF.Atan2(yawY, yawX);
        float rollY = 2 * ((rotation.W * rotation.Z) + (rotation.X * rotation.Y));
        float rollX = 1 - (2 * ((rotation.X * rotation.X) + (rotation.Z * rotation.Z)));
        float roll = MathF.Atan2(rollY, rollX);
        return new Vector3(float.RadiansToDegrees(pitch), float.RadiansToDegrees(yaw), float.RadiansToDegrees(roll));
    }

    private Quaternion ToQuaternion()
    {
        float yaw = float.DegreesToRadians((float)Y);
        float pitch = float.DegreesToRadians((float)X);
        float roll = float.DegreesToRadians((float)Z);
        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
    }

    private void WriteRotation()
    {
        Quaternion rotation = ToQuaternion();
        Write(rotation);
    }
}
