using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// The push constants of Shaders/Scene.hlsl: the frame's buffer and the number of the draw's record in it, and for a batch's part
/// its copies and the list of those the view sees, else <see cref="None"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SceneConstants(uint Frame, uint Draw, uint Copies = SceneConstants.None, uint Visible = SceneConstants.None)
{
    /// <summary>No buffer, as the shaders' None.</summary>
    public const uint None = uint.MaxValue;
}
