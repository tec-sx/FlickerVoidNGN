namespace FlickerVoid.Engine.Rendering;

/// <summary>An imported model: its mesh parts and the animations of its skin.</summary>
public sealed record ModelData(IReadOnlyList<MeshPart> Parts, IReadOnlyList<AnimationClip> Animations);
