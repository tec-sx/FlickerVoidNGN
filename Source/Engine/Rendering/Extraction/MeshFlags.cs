namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>How a mesh is drawn beyond its looks: unseen but for its shadows, as an outline alone, never cut away, or black and white.</summary>
[Flags]
public enum MeshFlags
{
    None = 0,

    /// <summary>Drawn into shadows alone (<see cref="Extraction.ShadowOnly"/>).</summary>
    ShadowOnly = 1,

    /// <summary>Drawn only as its outline, never seen or casting shadows, such as a doorway.</summary>
    OutlineOnly = 2,

    /// <summary>Kept where the cutaway would cut it (<see cref="Extraction.Uncut"/>).</summary>
    Uncut = 4,

    /// <summary>Drawn black and white (<see cref="Extraction.Greyed"/>).</summary>
    Greyed = 8,

    /// <summary>
    /// An instance batch's <see cref="Instancing.InstanceBatch.ShadowMesh"/>: drawn only into shadow maps, in place of the batch's
    /// model, never seen and never traced.
    /// </summary>
    ShadowMap = 16,
}
