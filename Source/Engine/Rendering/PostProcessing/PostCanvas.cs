using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The canvas post-processing lays the scene on: its file's settings, its loaded image (none paints on nothing) and how the camera sees it.</summary>
internal readonly record struct PostCanvas(CanvasFile File, ITexture? Image, CanvasView View);
