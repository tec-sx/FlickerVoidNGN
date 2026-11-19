using FlickerVoid.Engine.Builder;
using FlickerVoid.Engine.Builder.Importing;

// Builds a content folder: FlickerVoid.Engine.Builder <source folder> <output folder>. Exits with 1 when a file fails.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: FlickerVoid.Engine.Builder <source folder> <output folder>");
    return 2;
}

IAssetImporter[] importers = [];
AssetBuilder builder = new(importers);
BuildReport report = builder.Build(args[0], args[1]);
foreach (BuildFailure failure in report.Failed)
{
    Console.Error.WriteLine($"{failure.Path}: error: {failure.Message}");
}

Console.WriteLine($"Content: {report.Built.Count} built, {report.Skipped.Count} up to date, {report.Removed.Count} removed, {report.Failed.Count} failed.");
if (report.Failed.Count > 0)
{
    return 1;
}

return 0;
