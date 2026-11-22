using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Cameras.Transitions;

/// <summary>The push constants of Shaders/Transition.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TransitionConstants(uint DissolveTexture, float Dissolve, float Fade, uint Padding, Vector4 FadeColor);
