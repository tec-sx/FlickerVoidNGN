namespace FlickerVoid.Engine.Rendering;

/// <summary>A model's parts on the GPU and its skin's animations.</summary>
internal sealed record GpuModel(IReadOnlyList<GpuMeshPart> Parts, IReadOnlyList<AnimationClip> Animations);
