using System.Numerics;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;

using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

namespace FlickerVoid.Editor.Assets.Vehicles;

/// <summary>
/// Writes a vehicle's greyboxes as a <c>.glb</c> for Blender (milestone 3 of the creation tools): a node named after the vehicle with
/// an object per part under it, named by the part's id with the id again in its <c>fv_part</c> property, its origin at the part's
/// pivot. glTF holds triangles only, so faces come out triangulated.
/// </summary>
public static class VehicleExport
{
    /// <summary>The custom property naming a part, which Blender keeps as an object's custom property and the builder reads back.</summary>
    public const string PartProperty = "fv_part";

    /// <summary>The vehicle's greyboxes as the bytes of a <c>.glb</c> whose root node has the name.</summary>
    public static byte[] ToGlb(VehicleFile vehicle, string name)
    {
        ModelRoot model = ToGltf(vehicle, name);
        ArraySegment<byte> bytes = model.WriteGLB();
        return [.. bytes];
    }

    public static ModelRoot ToGltf(VehicleFile vehicle, string name)
    {
        MaterialBuilder material = new("Greybox");
        Vector4 grey = new(0.5f, 0.5f, 0.5f, 1);
        material.WithBaseColor(grey);
        SceneBuilder scene = new();
        NodeBuilder root = new(name);
        foreach ((string id, VehiclePart part) in vehicle.GetParts())
        {
            NodeBuilder node = root.CreateNode(id);
            node.LocalMatrix = part.Place.ToMatrix();
            node.Extras = new JsonObject { [PartProperty] = id };
            MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> mesh = BuildMesh(id, part.Mesh, material);
            scene.AddRigidMesh(mesh, node);
        }

        return scene.ToGltf2();
    }

    // The engine winds front faces clockwise and glTF counter-clockwise, so each triangle turns round.
    private static MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> BuildMesh(string id, SolidMesh solid, MaterialBuilder material)
    {
        MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> mesh = new(id);
        PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty> primitive = mesh.UsePrimitive(material);
        SolidTriangles triangles = solid.Triangulate();
        for (int index = 0; index + 2 < triangles.Indices.Length; index += 3)
        {
            VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> a = ToVertex(triangles.Vertices[triangles.Indices[index]]);
            VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> b = ToVertex(triangles.Vertices[triangles.Indices[index + 1]]);
            VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> c = ToVertex(triangles.Vertices[triangles.Indices[index + 2]]);
            primitive.AddTriangle(a, c, b);
        }

        return mesh;
    }

    private static VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ToVertex(SolidVertex vertex)
    {
        VertexPositionNormal geometry = new(vertex.Position, vertex.Normal);
        VertexTexture1 texture = new(vertex.TextureCoordinates);
        return new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(geometry, texture, default(VertexEmpty));
    }
}
