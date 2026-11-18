using System.Runtime.InteropServices;

using Silk.NET.Core.Loader;

namespace FlickerVoid.Engine.Platform;

/// <summary>
/// Lets Silk.NET find native libraries in the app's runtimes/&lt;os&gt;-&lt;arch&gt;/native folder. Silk guesses that folder
/// from the distribution's runtime identifier and misses it on distributions it doesn't list, such as Ubuntu 26.04.
/// </summary>
public static class NativeLibraryPaths
{
    private static int _added;

    /// <summary>Adds the lookup once; call it before Silk loads its first native library.</summary>
    public static void AddRuntimesFolder()
    {
        if (Interlocked.Exchange(ref _added, 1) == 1)
        {
            return;
        }

        if (PathResolver.Default is DefaultPathResolver resolver)
        {
            resolver.Resolvers.Add(FindInRuntimesFolder);
        }
    }

    private static IEnumerable<string> FindInRuntimesFolder(string name)
    {
        string fileName = Path.GetFileName(name);
        string runtimeIdentifier = GetPortableRuntimeIdentifier();
        string path = Path.Combine(AppContext.BaseDirectory, "runtimes", runtimeIdentifier, "native", fileName);
        if (File.Exists(path))
        {
            return [path];
        }

        return [];
    }

    /// <summary>The runtime identifier NuGet packages ship natives under, such as linux-x64 or win-arm64.</summary>
    private static string GetPortableRuntimeIdentifier()
    {
        string os = "linux";
        if (OperatingSystem.IsWindows())
        {
            os = "win";
        }
        else if (OperatingSystem.IsMacOS())
        {
            os = "osx";
        }

        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return $"{os}-{architecture}";
    }
}
