using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>How a mesh part looks: a base color, multiplied by its texture when it has one.</summary>
public sealed record MaterialData(Vector4 BaseColor, TextureData? BaseColorTexture);

