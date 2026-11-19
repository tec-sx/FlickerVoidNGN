using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Builder.Shaders;

/// <summary>A shader asset file, <c>Name.shader.json</c>: the entry points of <c>Name.hlsl</c> to compile, with their stages.</summary>
public sealed record ShaderAssetData(Dictionary<string, ShaderStage> EntryPoints);
