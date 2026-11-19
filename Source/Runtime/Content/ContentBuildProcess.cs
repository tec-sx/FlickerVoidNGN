using System.Diagnostics;
using System.Globalization;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>
/// Runs the builder in watch mode beside a development build, so saving a source file rebuilds it into the content folder,
/// where <see cref="ContentWatcher"/> reloads it. The builder stops with the game.
/// </summary>
public sealed class ContentBuildProcess : IDisposable
{
    private readonly Process _process;

    private ContentBuildProcess(Process process)
    {
        _process = process;
    }

    /// <summary>Starts the builder watching the manifest's folders; returns null if the process couldn't start.</summary>
    public static ContentBuildProcess? Start(ContentBuildManifest manifest, string manifestPath)
    {
        ProcessStartInfo start = new(manifest.Dotnet) { UseShellExecute = false };
        start.ArgumentList.Add(manifest.Builder);
        start.ArgumentList.Add("--watch");
        start.ArgumentList.Add(manifestPath);
        start.ArgumentList.Add("--parent");
        string parent = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        start.ArgumentList.Add(parent);
        Process? process = Process.Start(start);
        if (process is null)
        {
            return null;
        }

        return new ContentBuildProcess(process);
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
    }
}
