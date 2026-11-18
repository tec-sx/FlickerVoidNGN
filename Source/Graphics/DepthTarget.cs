namespace FlickerVoid.Engine.Graphics;

/// <summary>The depth texture a render pass tests against, and how it starts.</summary>
public readonly record struct DepthTarget(ITexture Texture, LoadAction Load = LoadAction.Load, float ClearDepth = 1);
