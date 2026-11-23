using System.Reflection;
using System.Runtime.Loader;

namespace FlickerVoid.Editor.Core.Projects;

/// <summary>
/// Loads a project's built game beside the editor: the project's own assemblies come from the game's output folder, while the engine
/// and the libraries the editor already has are the editor's, so both share the engine's types. It is collectible, and reads
/// the game's assemblies into memory, so their files stay free for the next build.
/// </summary>
internal sealed class GameLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public GameLoadContext(string program)
        : base(Path.GetFileNameWithoutExtension(program), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(program);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException)
        {
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            return null;
        }

        return LoadCopy(path);
    }

    /// <summary>Loads the assembly at <paramref name="path"/>, and its symbols when they are beside it, from copies in memory.</summary>
    public Assembly LoadCopy(string path)
    {
        byte[] assembly = File.ReadAllBytes(path);
        using MemoryStream assemblyStream = new(assembly);
        string symbolsPath = Path.ChangeExtension(path, ".pdb");
        if (!File.Exists(symbolsPath))
        {
            return LoadFromStream(assemblyStream);
        }

        byte[] symbols = File.ReadAllBytes(symbolsPath);
        using MemoryStream symbolsStream = new(symbols);
        return LoadFromStream(assemblyStream, symbolsStream);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (path is null)
        {
            return IntPtr.Zero;
        }

        return LoadUnmanagedDllFromPath(path);
    }
}
