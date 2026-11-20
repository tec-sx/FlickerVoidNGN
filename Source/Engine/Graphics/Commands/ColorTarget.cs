using System.Numerics;

using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Commands;

/// <summary>A texture a render pass draws to, and how it starts.</summary>
public readonly record struct ColorTarget(ITexture Texture, LoadAction Load = LoadAction.Load, Vector4 ClearColor = default);

