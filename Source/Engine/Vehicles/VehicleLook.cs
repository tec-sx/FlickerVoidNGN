using System.Numerics;
using System.Text.RegularExpressions;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// A vehicle's look from Blender bound to its parts (milestone 3 of the creation tools). Each object of the look belongs to the part
/// its <c>fv_part</c> property or its name names (Blender's <c>.001</c> suffixes ignored), else to the part of the nearest node it
/// hangs under, else to the body, and is reported. Each part's meshes are moved into its pivot's space, so the editor's pivots win
/// wherever Blender put the origins. A part with no object keeps its greybox.
/// </summary>
public sealed partial class VehicleLook
{
    /// <summary>The look's file beside the vehicle's: <c>Van.glb</c> for <c>Van.fvvehicle</c>.</summary>
    public const string Extension = ".glb";

    private VehicleLook(IReadOnlyDictionary<string, ModelData> parts, IReadOnlyList<string> unmatched, IReadOnlyList<string> greyboxed)
    {
        Parts = parts;
        Unmatched = unmatched;
        Greyboxed = greyboxed;
    }

    /// <summary>The look of each part that has one, by part id, in the part's pivot space.</summary>
    public IReadOnlyDictionary<string, ModelData> Parts { get; }

    /// <summary>The names of the objects that matched no part and joined the body.</summary>
    public IReadOnlyList<string> Unmatched { get; }

    /// <summary>The ids of the parts the look has no object for, which keep their greybox.</summary>
    public IReadOnlyList<string> Greyboxed { get; }

    /// <summary>The look's source file for the vehicle file at the content path.</summary>
    public static string GetPath(string vehiclePath) => Path.ChangeExtension(vehiclePath, Extension);

    public static VehicleLook Bind(VehicleFile vehicle, ModelData look)
    {
        IReadOnlyList<(string Id, VehiclePart Part)> parts = vehicle.GetParts();
        Dictionary<string, VehiclePart> byId = parts.ToDictionary(part => part.Id, part => part.Part);
        Dictionary<string, List<MeshPart>> meshes = [];
        List<string> unmatched = [];
        foreach (MeshPart mesh in look.Parts)
        {
            string? id = Match(mesh.Nodes, byId);
            if (id is null)
            {
                id = VehicleFile.BodyId;
                string name = mesh.Nodes.Count > 0 ? mesh.Nodes[0] : string.Empty;
                unmatched.Add(name);
            }

            Matrix4x4 place = byId[id].Place.ToMatrix();
            MeshPart moved = IntoPivot(mesh, place);
            if (!meshes.TryGetValue(id, out List<MeshPart>? list))
            {
                list = [];
                meshes.Add(id, list);
            }

            list.Add(moved);
        }

        Dictionary<string, ModelData> bound = meshes.ToDictionary(pair => pair.Key, pair => new ModelData(pair.Value, []));
        List<string> greyboxed = [.. parts.Select(part => part.Id).Where(id => !bound.ContainsKey(id))];
        return new VehicleLook(bound, unmatched, greyboxed);
    }

    /// <summary>The first of the nodes, innermost first, that names a part, or null.</summary>
    private static string? Match(IReadOnlyList<string> nodes, Dictionary<string, VehiclePart> parts)
    {
        foreach (string node in nodes)
        {
            string name = NumberSuffix().Replace(node, string.Empty);
            if (parts.ContainsKey(name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Takes a mesh from the vehicle's space into the space of the pivot placed by <paramref name="place"/>.</summary>
    private static MeshPart IntoPivot(MeshPart mesh, Matrix4x4 place)
    {
        Matrix4x4.Invert(place, out Matrix4x4 inverse);
        Matrix4x4 normals = Matrix4x4.Transpose(place);
        MeshVertex[] vertices = new MeshVertex[mesh.Vertices.Length];
        for (int index = 0; index < vertices.Length; index++)
        {
            MeshVertex vertex = mesh.Vertices[index];
            Vector3 position = Vector3.Transform(vertex.Position, inverse);
            Vector3 normal = Vector3.TransformNormal(vertex.Normal, normals);
            if (normal != Vector3.Zero)
            {
                normal = Vector3.Normalize(normal);
            }

            vertices[index] = vertex with { Position = position, Normal = normal };
        }

        uint[] indices = mesh.Indices;
        if (place.GetDeterminant() < 0)
        {
            indices = [.. indices];
            for (int index = 0; index + 2 < indices.Length; index += 3)
            {
                (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
            }
        }

        return mesh with { Vertices = vertices, Indices = indices, Skin = null };
    }

    [GeneratedRegex(@"\.\d{3,}$")]
    private static partial Regex NumberSuffix();
}
