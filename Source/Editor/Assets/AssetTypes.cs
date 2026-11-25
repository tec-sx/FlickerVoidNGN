using System.Text;
using System.Text.Json;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Data;
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

    public static AssetType Code { get; } = new("Code", "Code", "#6FC2B0", ".cs");

    /// <summary>Every type a file can have, in the order the browser's filter and New menu list them.</summary>
    public static IReadOnlyList<AssetType> Files { get; } = [Other, Scene, Vehicle, Model, Material, Texture, Mood, Canvas, Shader, Data, Table, Flow, Code];

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
