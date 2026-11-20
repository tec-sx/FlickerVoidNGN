using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Editor.Core.Overlays;

/// <summary>One icon as Icon.hlsl reads it: where, how many pixels across, its tint and the bindless index of its texture.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct OverlayIconData(Vector3 Position, float SizePixels, Vector4 Color, uint Texture, uint Padding0 = 0, uint Padding1 = 0, uint Padding2 = 0);
