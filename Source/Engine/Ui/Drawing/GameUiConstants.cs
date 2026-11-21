using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Ui.Drawing;

/// <summary>The push constants of Shaders/GameUi.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GameUiConstants(uint Texture, uint LinearOutput);
