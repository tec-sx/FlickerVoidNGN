namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws a glTF model at its entity's transform.</summary>
public struct MeshRenderer
{
    /// <summary>The .glb or .gltf file's path in the content folder.</summary>
    public required string Mesh { get; set; }
}

