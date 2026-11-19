using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Devices;

namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>Push constants one word past the limit.</summary>
[StructLayout(LayoutKind.Sequential, Size = GraphicsLimits.PushConstantSize + 4)]
internal struct TooLargeConstants;
