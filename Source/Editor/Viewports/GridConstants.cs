using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>The grid shader's push constants; mirrors GridConstants in Grid.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GridConstants(Matrix4x4 InverseViewProjection, Vector4 DepthColumn, Vector4 WColumn, float CellSize, float MajorCellSize, float Opacity, float Padding = 0);
