using System.Reflection;
using System.Runtime.InteropServices;

namespace FlickerVoid.Editor.Core.Meshes.Booleans;

/// <summary>The calls of Manifold's C binding (<c>manifoldc</c>) the Booleans make, over its raw pointers.</summary>
internal static partial class ManifoldNative
{
    public const string Library = "manifoldc";

    private static readonly Lock Gate = new();
    private static bool _registered;

    /// <summary>Lets the library be found beside the program, or in the engine's <c>artifacts/native</c> folder above it, as built by <c>Source/Native/Manifold</c>.</summary>
    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            NativeLibrary.SetDllImportResolver(typeof(ManifoldNative).Assembly, Resolve);
        }
    }

    [LibraryImport(Library)]
    public static partial nuint manifold_manifold_size();

    [LibraryImport(Library)]
    public static partial nuint manifold_meshgl_size();

    [LibraryImport(Library)]
    public static partial nint manifold_alloc_manifold();

    [LibraryImport(Library)]
    public static partial nint manifold_alloc_meshgl();

    [LibraryImport(Library)]
    public static partial void manifold_delete_manifold(nint manifold);

    [LibraryImport(Library)]
    public static partial void manifold_delete_meshgl(nint meshgl);

    [LibraryImport(Library)]
    public static partial nint manifold_meshgl(nint mem, float[] vertexProperties, nuint vertexCount, nuint propertyCount, uint[] triangleVertices, nuint triangleCount);

    [LibraryImport(Library)]
    public static partial nint manifold_of_meshgl(nint mem, nint meshgl);

    [LibraryImport(Library)]
    public static partial int manifold_status(nint manifold);

    [LibraryImport(Library)]
    public static partial nint manifold_boolean(nint mem, nint first, nint second, int operation);

    [LibraryImport(Library)]
    public static partial nint manifold_get_meshgl(nint mem, nint manifold);

    [LibraryImport(Library)]
    public static partial nuint manifold_meshgl_num_prop(nint meshgl);

    [LibraryImport(Library)]
    public static partial nuint manifold_meshgl_vert_properties_length(nint meshgl);

    [LibraryImport(Library)]
    public static partial nuint manifold_meshgl_tri_length(nint meshgl);

    [LibraryImport(Library)]
    public static partial nint manifold_meshgl_vert_properties(nint mem, nint meshgl);

    [LibraryImport(Library)]
    public static partial nint manifold_meshgl_tri_verts(nint mem, nint meshgl);

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Library)
        {
            return 0;
        }

        foreach (string folder in GetFolders())
        {
            foreach (string file in GetFileNames())
            {
                string path = Path.Combine(folder, file);
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out nint handle))
                {
                    return handle;
                }
            }
        }

        return 0;
    }

    private static IEnumerable<string> GetFolders()
    {
        yield return AppContext.BaseDirectory;
        string rid = "linux-x64";
        if (OperatingSystem.IsWindows())
        {
            rid = "win-x64";
        }

        DirectoryInfo? above = new(AppContext.BaseDirectory);
        while (above is not null)
        {
            yield return Path.Combine(above.FullName, "artifacts", "native", rid);
            above = above.Parent;
        }
    }

    private static IEnumerable<string> GetFileNames()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return "manifoldc.dll";
            yield break;
        }

        yield return "libmanifoldc.so";
    }
}
