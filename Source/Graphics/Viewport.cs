namespace FlickerVoid.Engine.Graphics;

/// <summary>The render target area a draw maps clip space to, with y going down from the top left.</summary>
public readonly record struct Viewport(float X, float Y, float Width, float Height, float MinDepth = 0, float MaxDepth = 1);
