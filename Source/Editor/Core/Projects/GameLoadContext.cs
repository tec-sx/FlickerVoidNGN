using System.Reflection;
using System.Runtime.Loader;

namespace FlickerVoid.Editor.Core.Projects;

/// <summary>
/// Loads a project's built game beside the editor: the project's own assemblies come from the game's output folder, while the engine
/// and the libraries the editor already has are the editor's, so both share the engine's types.
/// </summary>
internal sealed class GameLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public GameLoadContext(string program)
        : base(Path.GetFileNameWithoutExtension(program))
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

        return LoadFromAssemblyPath(path);
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
