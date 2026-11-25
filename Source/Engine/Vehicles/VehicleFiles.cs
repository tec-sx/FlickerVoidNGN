using System.Text.Json;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Vehicles.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>Reads vehicle files through the content, keeping the last version of each that could be read, so a file broken while it is edited leaves its vehicles as they were.</summary>
internal sealed partial class VehicleFiles(ILogger logger)
{
    private readonly Dictionary<string, VehicleFile> _read = new(ContentManager.PathComparer);

    /// <summary>The file as it is now, else as it last could be read; null when it never could.</summary>
    public VehicleFile? Read(IContentManager content, string path)
    {
        try
        {
            VehicleFile file = content.Load(path, VehiclesJsonContext.Default.VehicleFile);
            _read[path] = file;
            return file;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException)
        {
            LogUnreadable(path, exception.Message);
            return _read.GetValueOrDefault(path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Path} can't be read, so its vehicles keep their last parts: {Problem}")]
    private partial void LogUnreadable(string path, string problem);
}
