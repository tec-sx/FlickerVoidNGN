using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Weather;

/// <summary>
/// The push constants of Shaders/Weather.hlsl: the frame's buffer, which particles (rain or dust) and how many, the time they move
/// by, the box they fill, how bright they are, a streak's length, their velocity and their width in render pixels.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct WeatherConstants(
    uint Frame,
    uint Kind,
    uint Count,
    float Time,
    Vector3 Center,
    float Brightness,
    Vector3 Size,
    float Length,
    Vector3 Velocity,
    float Width);
