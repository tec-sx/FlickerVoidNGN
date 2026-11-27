using System.Text.Json;

using FlickerVoid.Engine.Foliage.Serialization;

namespace FlickerVoid.Engine.Foliage.Tests;

/// <summary>Writes and reads foliage files as the content folder holds them.</summary>
internal static class FoliageFileTestsBuilder
{
    public static string Write(FoliageFile foliage) => JsonSerializer.Serialize(foliage, FoliageJsonContext.Default.FoliageFile);

    public static FoliageFile Read(string json)
    {
        FoliageFile? foliage = JsonSerializer.Deserialize(json, FoliageJsonContext.Default.FoliageFile);
        return foliage ?? throw new InvalidDataException("The file holds null.");
    }
}
