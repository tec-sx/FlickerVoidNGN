using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// The pipelines a pass draws masters' materials with, one a master, made from its compiled pixel shader the first time it is
/// asked for, without culling when it is two-sided, and made again after the content build changes the shader or the master's file.
/// </summary>
internal sealed class MasterPipelines : IDisposable
{
    private readonly IContentManager _content;
    private readonly Func<ShaderBytecode, CullMode, string, IPipeline> _create;
    private readonly Dictionary<string, IPipeline> _pipelines = new(ContentManager.PathComparer);
    private readonly Dictionary<string, string> _files = new(ContentManager.PathComparer);

    /// <param name="create">Makes a master's pipeline from its pixel shader, how it culls and its master's path.</param>
    public MasterPipelines(IContentManager content, Func<ShaderBytecode, CullMode, string, IPipeline> create)
    {
        _content = content;
        _create = create;
        _content.Changed += HandleChanged;
    }

    public IPipeline Get(string master)
    {
        if (_pipelines.TryGetValue(master, out IPipeline? made))
        {
            return made;
        }

        MasterFile file = _content.Load(master, RenderingJsonContext.Default.MasterFile);
        string entryPoint = file.EntryPoint;
        string shaderPath = MasterFile.GetShaderPath(master, entryPoint);
        string shaderFullPath = _content.GetFullPath(shaderPath);
        byte[] code = File.ReadAllBytes(shaderFullPath);
        ShaderBytecode pixelShader = new(code, entryPoint);
        CullMode cull = CullMode.Back;
        if (file.TwoSided)
        {
            cull = CullMode.None;
        }

        IPipeline pipeline = _create(pixelShader, cull, master);
        _pipelines.Add(master, pipeline);
        _files[shaderFullPath] = master;
        string masterFullPath = _content.GetFullPath(master);
        _files[masterFullPath] = master;
        return pipeline;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
        foreach (IPipeline pipeline in _pipelines.Values)
        {
            pipeline.Dispose();
        }

        _pipelines.Clear();
    }

    // The device destroys a replaced pipeline once the GPU is done with it.
    private void HandleChanged(string fullPath)
    {
        if (_files.TryGetValue(fullPath, out string? master) && _pipelines.Remove(master, out IPipeline? pipeline))
        {
            pipeline.Dispose();
        }
    }
}
