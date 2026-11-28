using System.Text;
using System.Text.Json;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Serialization;
using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;
using FlickerVoid.Engine.Props;
using FlickerVoid.Engine.Props.Serialization;
using FlickerVoid.Engine.Foliage;
using FlickerVoid.Engine.Foliage.Serialization;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Editor.Assets;

/// <summary>The kinds of content file the editor knows, each in one place: a new file type is one more <see cref="AssetType"/> here.</summary>
public static class AssetTypes
{
    private const string LookGroup = "Look";
    private const string FlowGroup = "Flow";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    /// <summary>A file of no type the editor knows.</summary>
    public static AssetType Other { get; } = new("Other", "File", "#9C9A88");

    public static AssetType Folder { get; } = new("Folder", "Folder", "#DCBC98");

    public static AssetType Scene { get; } = new("Scene", "Scene", "#A6C33F", ".fvscene")
    {
        New = new AssetNew("Scene", "New Scene", "scene", AssetNew.Json("\"entities\": []")),
    };

    /// <summary>A vehicle creation: New makes a car on four wheels.</summary>
    public static AssetType Vehicle { get; } = new("Vehicle", "Vehicle", "#D97BA6", VehicleFile.Extension)
    {
        New = new AssetNew("Vehicle", "New Vehicle", "vehicle", WriteVehicle),
        IsCreation = true,
    };

    /// <summary>A building creation: New makes a ground floor with an entrance, under a roof.</summary>
    public static AssetType Building { get; } = new("Building", "Building", "#C9A36B", BuildingFile.Extension)
    {
        New = new AssetNew("Building", "New Building", "building", WriteBuilding),
        IsCreation = true,
    };

    /// <summary>A prop: an object with no rules of its own, made of boxes; New makes one metre box.</summary>
    public static AssetType Prop { get; } = new("Prop", "Prop", "#B5A58C", PropFile.Extension)
    {
        New = new AssetNew("Prop", "New Prop", "prop", WriteProp),
        IsCreation = true,
    };

    /// <summary>A foliage creation: New makes a tree grown from a seed, which its creator can turn into a bush.</summary>
    public static AssetType Foliage { get; } = new("Foliage", "Tree", "#7FB069", FoliageFile.Extension)
    {
        New = new AssetNew("Tree", "New Tree", "foliage", WriteFoliage),
        IsCreation = true,
    };

    public static AssetType Model { get; } = new("Model", "Model", "#4A9BBF", ".glb", ".gltf", ".fvmodel");

    public static AssetType Material { get; } = new("Material", "Materials", "#D8955A", ".fvmat")
    {
        New = new AssetNew("Material", "New Material", "material", AssetNew.Json("\"parameters\": {}")) { Group = LookGroup },
    };

    public static AssetType Texture { get; } = new("Texture", "Texture", "#C8553D", ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".reb", ".dds");

    public static AssetType Mood { get; } = new("Mood", "Mood", "#E0AC2C", ".fvmood")
    {
        New = new AssetNew("Mood", "New Mood", "mood", AssetNew.Json(null)) { Group = LookGroup },
    };

    /// <summary>A place's light and air over the day, edited in the environment editor.</summary>
    public static AssetType Environment { get; } = new("Environment", "Mood", "#D9B23A", LightingEnvironment.Extension)
    {
        New = new AssetNew("Environment", "New Environment", "environment", AssetNew.Json("\"numbers\": {},\n  \"colors\": {}")) { Group = LookGroup },
    };

    /// <summary>The glass the main camera draws with: bloom, tonemapper, canvas and depth fade.</summary>
    public static AssetType Lens { get; } = new("Lens", "Camera", "#9FC7D8", CameraLens.Extension)
    {
        New = new AssetNew("Lens", "New Lens", "lens", AssetNew.Json(null)) { Group = LookGroup },
    };

    public static AssetType Canvas { get; } = new("Canvas", "Canvas", "#E8DCC0", ".fvcanvas")
    {
        New = new AssetNew("Canvas", "New Canvas", "canvas", AssetNew.Json(null)) { Group = LookGroup },
    };

    public static AssetType Shader { get; } = new("Shader", "Shader", "#B9C5CF", ".fvshader", ".hlsl", ".hlsli", ".spv");

    public static AssetType Data { get; } = new("Data", "Data", "#8FB8A0", DataAssets.AssetExtension);

    public static AssetType Table { get; } = new("Table", "Table", "#7FA6C9", DataAssets.TableExtension);

    public static AssetType Flow { get; } = new("Flow", "Flow", "#B48EC9", FlowGraph.Extension)
    {
        New = new AssetNew("Flow Graph", "New Flow", "flow", AssetNew.Json("\"nodes\": [ { \"$type\": \"start\", \"id\": \"start\" } ],\n  \"links\": []")) { Group = FlowGroup },
    };

    /// <summary>A layered music cue or an ambience bed, edited in the music editor.</summary>
    public static AssetType Music { get; } = new("Music", "Music", "#E58F65", MusicCue.Extension);

    /// <summary>A sound: a clip, or a few to pick from, with gain, pitch, spread and the bus it plays on.</summary>
    public static AssetType Sound { get; } = new("Sound", "Waveform", "#7FC4B8", SoundDefinition.Extension);

    /// <summary>A recorded or exported sound file, imported into Content; sounds and music cues play them.</summary>
    public static AssetType Audio { get; } = new("Audio", "Waveform", "#B8A27F", ".wav", ".ogg");

    /// <summary>The mix states of a game: what lowers and dulls which bus while the player is indoors, talking or in the hub.</summary>
    public static AssetType Mix { get; } = new("Mix", "Mixer", "#D9A66B", MixSet.Extension)
    {
        New = new AssetNew("Mix", "Mix", "data", WriteMix),
    };

    public static AssetType Code { get; } = new("Code", "Code", "#6FC2B0", ".cs");

    /// <summary>Every type a file can have, in the order the browser's filter and New menu list them.</summary>
    public static IReadOnlyList<AssetType> Files { get; } = [Other, Scene, Vehicle, Building, Prop, Foliage, Model, Material, Texture, Mood, Environment, Lens, Canvas, Shader, Data, Table, Flow, Music, Sound, Audio, Mix, Code];

    /// <summary>The content drawer's filter chips (rule R11, categories before long lists): everything, then five kinds of work.</summary>
    public static IReadOnlyList<AssetFilter> Filters { get; } =
    [
        new("All", []),
        new("Scenes", [Scene, Vehicle, Building, Prop, Foliage], Scene.Color),
        new("Art", [Model, Material, Texture, Canvas, Shader], Model.Color),
        new("Light", [Mood, Environment, Lens], Mood.Color),
        new("Sound", [Music, Sound, Audio, Mix], Music.Color),
        new("Data", [Data, Table, Flow], Flow.Color),
        new("Code", [Code], Code.Color),
    ];

    /// <summary>The type of the file at the path, by its extension; <see cref="Other"/> for none the editor knows.</summary>
    public static AssetType Find(string path)
    {
        foreach (AssetType type in Files)
        {
            if (type.Marks(path))
            {
                return type;
            }
        }

        return Other;
    }

    private static string WriteMix(string schemaPath)
    {
        MixSet mix = MixSet.Default();
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, mix, AudioJsonContext.Default.MixSet);
        }

        string body = Encoding.UTF8.GetString(stream.ToArray());
        string header = "{\n  \"$schema\": \"" + schemaPath + "\",\n";
        return header + body.TrimStart('{').TrimStart('\r', '\n') + "\n";
    }

    private static string WriteFoliage(string schemaPath)
    {
        FoliageFile tree = FoliageFile.Create(FoliageKind.Tree);
        tree.Schema = schemaPath;
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, tree, FoliageJsonContext.Default.FoliageFile);
        }

        byte[] written = stream.ToArray();
        return Encoding.UTF8.GetString(written) + "\n";
    }

    private static string WriteProp(string schemaPath)
    {
        PropFile prop = PropFile.Create();
        prop.Schema = schemaPath;
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, prop, PropsJsonContext.Default.PropFile);
        }

        byte[] written = stream.ToArray();
        return Encoding.UTF8.GetString(written) + "\n";
    }

    private static string WriteBuilding(string schemaPath)
    {
        BuildingFile building = BuildingFile.Create();
        BuildingTools.Rebuild(building);
        building.Schema = schemaPath;
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, building, BuildingsJsonContext.Default.BuildingFile);
        }

        byte[] written = stream.ToArray();
        return Encoding.UTF8.GetString(written) + "\n";
    }

    private static string WriteVehicle(string schemaPath)
    {
        VehicleFile vehicle = VehicleFile.Create(VehicleType.Car);
        vehicle.Schema = schemaPath;
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, vehicle, VehiclesJsonContext.Default.VehicleFile);
        }

        byte[] written = stream.ToArray();
        return Encoding.UTF8.GetString(written) + "\n";
    }
}
