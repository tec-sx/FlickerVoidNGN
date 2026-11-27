using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Props;

/// <summary>
/// A <c>Name.fvprop</c> file: an object with no rules of its own (a crate, a room, a car to look at), made of named solid parts the
/// creator adds and shapes, and doors the door tool places. Part ids: the parts' names, then <c>door.1</c>, <c>door.2</c>.
/// </summary>
public sealed class PropFile : ICreationFile
{
    public const string Extension = ".fvprop";

    private const string DoorPrefix = "door.";
    private const string PartPrefix = "part.";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>The parts in the order they were added; each name is its id.</summary>
    public List<PropPart> Parts { get; set; } = [];

    /// <summary>The doors, numbered from 1 in order: <c>door.1</c>, <c>door.2</c>; the door tool adds them.</summary>
    public List<SolidPart> Doors { get; set; } = [];

    /// <summary>A new prop: one metre box standing on its origin.</summary>
    public static PropFile Create()
    {
        PropFile prop = new();
        prop.AddBox(Vector3.One, Vector3.Zero);
        return prop;
    }

    public IReadOnlyList<(string Id, SolidPart Part)> GetParts()
    {
        List<(string Id, SolidPart Part)> parts = [];
        foreach (PropPart part in Parts)
        {
            parts.Add((part.Name, part.Part));
        }

        for (int index = 0; index < Doors.Count; index++)
        {
            parts.Add(($"{DoorPrefix}{index + 1}", Doors[index]));
        }

        return parts;
    }

    /// <summary>Adds a box of the size standing at the point, named <c>part.N</c> with the first number free; its name.</summary>
    public string AddBox(Vector3 size, Vector3 at)
    {
        int number = 1;
        while (Parts.Any(part => part.Name == $"{PartPrefix}{number}"))
        {
            number++;
        }

        string name = $"{PartPrefix}{number}";
        SolidMesh box = SolidMesh.Box(size);
        SolidPart solid = new() { Place = new Transform { Position = at }, Mesh = box };
        Parts.Add(new PropPart { Name = name, Part = solid });
        return name;
    }

    /// <summary>Takes out the part with the name, with the doors placed on it; false for a name the prop lacks (doors are taken out by the door tool).</summary>
    public bool Remove(string name)
    {
        int index = Parts.FindIndex(part => part.Name == name);
        if (index < 0)
        {
            return false;
        }

        Parts.RemoveAt(index);
        Doors.RemoveAll(door => door.Fit?.Wall == name);
        return true;
    }
}
