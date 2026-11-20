using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws a glTF model at its entity's transform.</summary>
public struct MeshRenderer
{
    /// <summary>The .glb or .gltf file's path in the content folder.</summary>
    [ContentFile(".glb", ".gltf")]
    public required string Mesh { get; set; }

    /// <summary>
    /// A <c>.fvmat</c> file in the content folder for each mesh part, in the model's order, setting the scene
    /// shader's parameters; a part past the end of the list, or given an empty name, keeps the shader's defaults.
    /// </summary>
    [ContentFile(".fvmat")]
    public string[]? Materials { get; set; }
}

