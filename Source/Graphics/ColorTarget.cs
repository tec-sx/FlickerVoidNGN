using System.Numerics;

namespace FlickerVoid.Engine.Graphics;

/// <summary>A texture a render pass draws to, and how it starts.</summary>
public readonly record struct ColorTarget(ITexture Texture, LoadAction Load = LoadAction.Load, Vector4 ClearColor = default);
