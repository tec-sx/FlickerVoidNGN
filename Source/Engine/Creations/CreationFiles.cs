using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Content;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// Reads one kind's creation files through the content, keeping the last version of each that could be read, so a file broken while
/// it is edited leaves its creations as they were.
/// </summary>
public sealed partial class CreationFiles<T>(ILogger logger, JsonTypeInfo<T> type)
    where T : class
{
    private readonly Dictionary<string, T> _read = new(ContentManager.PathComparer);

    /// <summary>The file as it is now, else as it last could be read; null when it never could.</summary>
    public T? Read(IContentManager content, string path)
    {
        try
        {
            T file = content.Load(path, type);
            _read[path] = file;
            return file;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException)
        {
            LogUnreadable(path, exception.Message);
            return _read.GetValueOrDefault(path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Path} can't be read, so what is made from it keeps its last parts: {Problem}")]
    private partial void LogUnreadable(string path, string problem);
}
