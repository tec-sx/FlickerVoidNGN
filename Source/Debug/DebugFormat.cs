using System.Globalization;

namespace FlickerVoid.Engine.Debug;

/// <summary>Text for numbers the debug windows show.</summary>
public static class DebugFormat
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB"];

    /// <summary>A size in bytes in the largest unit that keeps it at 1 or more, with one decimal above bytes.</summary>
    public static string Bytes(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        if (unit == 0)
        {
            return $"{bytes} B";
        }

        string number = value.ToString("0.0", CultureInfo.InvariantCulture);
        return $"{number} {Units[unit]}";
    }
}
