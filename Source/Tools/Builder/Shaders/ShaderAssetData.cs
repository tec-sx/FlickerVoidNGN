using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Tools.ShaderCompiler;

namespace FlickerVoid.Tools.Builder.Shaders;

/// <summary>
/// A shader asset file, <c>Name.fvshader</c>: the entry points of <c>Name.hlsl</c> to compile, with their stages, and the
/// material parameters the shader declares, if any.
/// </summary>
public sealed record ShaderAssetData(Dictionary<string, ShaderStage> EntryPoints, List<ShaderParameter>? Parameters = null);
