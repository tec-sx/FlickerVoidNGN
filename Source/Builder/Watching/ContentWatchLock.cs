namespace FlickerVoid.Engine.Builder.Watching;

/// <summary>
/// Lets one builder at a time watch a content build manifest, so the editor and a game it plays from the same output folder
/// don't both rebuild every change: the holder keeps <c>&lt;manifest&gt;.lock</c> open with no sharing until it exits.
/// </summary>
public sealed class ContentWatchLock : IDisposable
{
    private readonly FileStream _file;

    private ContentWatchLock(FileStream file)
    {
        _file = file;
    }

    /// <summary>Takes the manifest's lock, or returns null while another watcher holds it.</summary>
    public static ContentWatchLock? TryTake(string manifestPath)
    {
        string path = manifestPath + ".lock";
        try
        {
            FileStream file = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            return new ContentWatchLock(file);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _file.Dispose();
    }
}
