namespace FlickerVoid.Engine.Graphics.Pipelines;

/// <summary>Loads compiled shaders, the <c>Name.EntryPoint.spv</c> files the content builder writes, from a folder.</summary>
public sealed class ShaderLibrary(string directory)
{
    public string Directory { get; } = directory;

    /// <summary>The shaders of an engine module, which the content build puts in Content/Engine/&lt;module&gt; next to the program.</summary>
    public static ShaderLibrary ForEngine(string module)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Content", "Engine", module);
        return new ShaderLibrary(directory);
    }

    public ShaderBytecode Load(string shader, string entryPoint)
    {
        string path = Path.Combine(Directory, $"{shader}.{entryPoint}.spv");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{path} doesn't exist. Shaders are compiled by the content build (FlickerVoidContent).", path);
        }

        byte[] code = File.ReadAllBytes(path);
        return new ShaderBytecode(code, entryPoint);
    }
}
