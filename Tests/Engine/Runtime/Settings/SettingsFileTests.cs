using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Settings;

namespace FlickerVoid.Engine.Runtime.Tests.Settings;

public class SettingsFileTests
{
    private enum Quality
    {
        Low,
        High,
    }

    private readonly SettingsFileTestsBuilder _builder = new();

    [Fact]
    public void AppliesTheValuesItNames()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        ConsoleVariable<Quality> quality = ConsoleVariable.CreateEnum("r.Quality", "How good.", Quality.Low);
        ConsoleVariable<float> scale = ConsoleVariable.Create("r.Scale", "How big.", 1f);
        IDebugConsole console = _builder.WithVariable(rayTracing).WithVariable(quality).WithVariable(scale).Build();
        const string Json = """
            {
              "$schema": ".schemas/settings.schema.json",
              "r.RayTracing": false,
              "R.QUALITY": "high",
              "r.Scale": 0.5
            }
            """;

        IReadOnlyList<string> problems = SettingsFile.Apply(Json, console);

        problems.Should().BeEmpty();
        rayTracing.Value.Should().BeFalse();
        quality.Value.Should().Be(Quality.High);
        scale.Value.Should().Be(0.5f);
    }

    [Fact]
    public void ResetsTheSavedValuesItLeavesOut()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        rayTracing.Value = false;
        IDebugConsole console = _builder.WithVariable(rayTracing).Build();

        IReadOnlyList<string> problems = SettingsFile.Apply("{}", console);

        problems.Should().BeEmpty();
        rayTracing.Value.Should().BeTrue();
    }

    [Fact]
    public void ReportsAndSkipsUnknownNamesAndBadValues()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        ConsoleVariable<Quality> quality = ConsoleVariable.CreateEnum("r.Quality", "How good.", Quality.Low);
        IDebugConsole console = _builder.WithVariable(rayTracing).WithVariable(quality).Build();
        const string Json = """
            {
              // Hand edits may carry comments and trailing commas.
              "r.Unknown": 1,
              "r.Quality": "ultra",
              "r.RayTracing": [true],
              "r.RayTracing": false,
            }
            """;

        IReadOnlyList<string> problems = SettingsFile.Apply(Json, console);

        problems.Should().Equal(
            "Unknown setting 'r.Unknown'.",
            "'ultra' is not one of Low, High for r.Quality.",
            "r.RayTracing must be a boolean, a number or a string.");
        rayTracing.Value.Should().BeFalse();
        quality.Value.Should().Be(Quality.Low);
    }

    [Fact]
    public void ReportsAMalformedFileAndLeavesTheDefaults()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        rayTracing.Value = false;
        IDebugConsole console = _builder.WithVariable(rayTracing).Build();

        IReadOnlyList<string> problems = SettingsFile.Apply("{ \"r.RayTracing\": ", console);

        problems.Should().ContainSingle().Which.Should().StartWith("The settings are not valid JSON:");
        rayTracing.Value.Should().BeTrue();
    }

    [Fact]
    public void WritesOnlyTheSavedValuesThatDifferFromTheirDefaults()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        ConsoleVariable<Quality> quality = ConsoleVariable.CreateEnum("r.Quality", "How good.", Quality.Low);
        ConsoleVariable<float> scale = ConsoleVariable.Create("r.Scale", "How big.", 1f);
        ConsoleVariable<int> view = new("r.View", "What to show.", 0, int.TryParse) { Saved = false };
        rayTracing.Value = false;
        quality.Value = Quality.High;
        view.Value = 2;
        IDebugConsole console = _builder.WithVariable(scale).WithVariable(rayTracing).WithVariable(quality).WithVariable(view).Build();

        string json = SettingsFile.Write(console, ".schemas/settings.schema.json");

        json.ReplaceLineEndings("\n").Should().Be("""
            {
              "$schema": ".schemas/settings.schema.json",
              "r.Quality": "High",
              "r.RayTracing": false
            }

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WhatItWritesReadsBack()
    {
        ConsoleVariable<float> scale = ConsoleVariable.Create("r.Scale", "How big.", 1f);
        scale.Value = 0.25f;
        IDebugConsole console = _builder.WithVariable(scale).Build();
        string json = SettingsFile.Write(console, "settings.schema.json");
        scale.Reset();

        IReadOnlyList<string> problems = SettingsFile.Apply(json, console);

        problems.Should().BeEmpty();
        scale.Value.Should().Be(0.25f);
    }

    [Fact]
    public void LoadingAMissingFileResetsToTheDefaults()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        rayTracing.Value = false;
        IDebugConsole console = _builder.WithVariable(rayTracing).Build();
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{SettingsFile.Extension}");

        IReadOnlyList<string> problems = SettingsFile.Load(path, console);

        problems.Should().BeEmpty();
        rayTracing.Value.Should().BeTrue();
    }

    [Fact]
    public void SavesNextToItsSchemaAndLoadsBack()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        IDebugConsole console = _builder.WithVariable(rayTracing).Build();
        string folder = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(folder, SettingsFile.FileName);
        rayTracing.Value = false;

        SettingsFile.Save(path, console);
        rayTracing.Reset();
        IReadOnlyList<string> problems = SettingsFile.Load(path, console);

        string json = File.ReadAllText(path);
        json.Should().Contain("\"$schema\": \".schemas/settings.schema.json\"");
        problems.Should().BeEmpty();
        rayTracing.Value.Should().BeFalse();
        Directory.Delete(folder, true);
    }

    [Fact]
    public void ItsSchemaListsTheSavedVariablesWithTheirHelpAndValues()
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", true);
        ConsoleVariable<Quality> quality = ConsoleVariable.CreateEnum("r.Quality", "How good.", Quality.Low);
        ConsoleVariable<int> view = new("r.View", "What to show.", 0, int.TryParse) { Saved = false };
        IDebugConsole console = _builder.WithVariable(rayTracing).WithVariable(quality).WithVariable(view).Build();

        JsonObject schema = SettingsFile.CreateSchema(console);

        JsonObject properties = schema["properties"]!.AsObject();
        properties.Select(property => property.Key).Should().BeEquivalentTo("$schema", "r.RayTracing", "r.Quality");
        properties["r.RayTracing"]!.ToJsonString().Should().Be("""{"description":"Traces rays.","type":"boolean","default":true}""");
        properties["r.Quality"]!.ToJsonString().Should().Be("""{"description":"How good.","type":"string","enum":["Low","High"],"default":"Low"}""");
        schema["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
    }
}
