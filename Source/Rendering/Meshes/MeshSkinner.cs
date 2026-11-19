using System.Numerics;
using System.Runtime.InteropServices;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Poses the skinned parts of animated meshes on the GPU each frame: it samples each mesh's animation, uploads the joint
/// matrices, and runs Shaders/Skinning.hlsl into vertex buffers that belong to the entity, which every pass then draws.
/// </summary>
internal sealed class MeshSkinner : IDisposable
{
    private const int GroupSize = 64;
    private const int MatrixSize = 64;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _pipeline;
    private readonly IBuffer?[] _joints = new IBuffer?[SceneRenderer.MaxFramesInFlight];
    private readonly List<Matrix4x4> _matrices = [];
    private readonly List<(GpuMeshPart Part, IBuffer Output, int JointsOffset)> _work = [];
    private readonly Dictionary<int, (GpuModel Model, IBuffer[] Buffers)> _posed = [];
    private readonly HashSet<int> _seen = [];
    private Matrix4x4[] _sample = [];

    public MeshSkinner(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _device = device;
        ComputePipelineDescription description = new()
        {
            ComputeShader = shaders.Load("Skinning", "ComputeMain"),
            Name = "Skinning",
        };
        _pipeline = device.CreateComputePipeline(description);
    }

    /// <summary>Starts a frame: forgets the meshes queued last frame.</summary>
    public void Begin()
    {
        _matrices.Clear();
        _work.Clear();
        _seen.Clear();
    }

    /// <summary>
    /// Queues the entity's skinned parts posed at the animator's time and returns the vertex buffer each part should be drawn
    /// from: the posed one for skinned parts, its own for the rest. Returns null when the model has no such animation, so
    /// the mesh draws in its bind pose.
    /// </summary>
    public IBuffer[]? Pose(int entity, GpuModel model, in MeshAnimator animator)
    {
        AnimationClip? clip = FindClip(model, animator.Animation);
        if (clip is null)
        {
            return null;
        }

        IBuffer[] buffers = GetPosedBuffers(entity, model);
        _seen.Add(entity);

        if (_sample.Length < clip.JointCount)
        {
            _sample = new Matrix4x4[clip.JointCount];
        }

        Span<Matrix4x4> sample = _sample.AsSpan(0, clip.JointCount);
        clip.Sample(animator.Time, sample);
        int jointsOffset = _matrices.Count * MatrixSize;
        foreach (Matrix4x4 matrix in sample)
        {
            _matrices.Add(matrix);
        }

        for (int index = 0; index < model.Parts.Count; index++)
        {
            GpuMeshPart part = model.Parts[index];
            if (part.Skin is not null)
            {
                _work.Add((part, buffers[index], jointsOffset));
            }
        }

        return buffers;
    }

    /// <summary>Uploads the joints into this frame's buffer and poses every queued part; releases the buffers of entities not seen.</summary>
    public void Dispatch(ICommandList commands, int frameSlot)
    {
        ReleaseUnseen();
        if (_work.Count == 0)
        {
            return;
        }

        IBuffer joints = GetJointBuffer(frameSlot, _matrices.Count * MatrixSize);
        ReadOnlySpan<Matrix4x4> matrices = CollectionsMarshal.AsSpan(_matrices);
        joints.Write(MemoryMarshal.AsBytes(matrices));

        commands.SetPipeline(_pipeline);
        foreach ((GpuMeshPart part, IBuffer output, int jointsOffset) in _work)
        {
            commands.Barrier(output, ResourceState.ShaderWrite);
            SkinningConstants constants = new(
                part.Vertices.BindlessIndex,
                part.Skin!.BindlessIndex,
                joints.BindlessIndex,
                (uint)jointsOffset,
                output.BindlessIndex,
                (uint)part.VertexCount);
            commands.PushConstants(constants);
            commands.Dispatch((part.VertexCount + GroupSize - 1) / GroupSize);
            commands.Barrier(output, ResourceState.ShaderRead);
        }
    }

    public void Dispose()
    {
        foreach (IBuffer? joints in _joints)
        {
            joints?.Dispose();
        }

        foreach ((GpuModel model, IBuffer[] buffers) in _posed.Values)
        {
            DisposePosed(model, buffers);
        }

        _posed.Clear();
        _pipeline.Dispose();
    }

    private static AnimationClip? FindClip(GpuModel model, string? name)
    {
        if (name is null)
        {
            return model.Animations.FirstOrDefault();
        }

        return model.Animations.FirstOrDefault(clip => clip.Name == name);
    }

    // Skinned parts get a buffer of their own to pose into; rigid parts keep their shared vertices.
    private IBuffer[] GetPosedBuffers(int entity, GpuModel model)
    {
        if (_posed.TryGetValue(entity, out (GpuModel Model, IBuffer[] Buffers) posed))
        {
            if (ReferenceEquals(posed.Model, model))
            {
                return posed.Buffers;
            }

            DisposePosed(posed.Model, posed.Buffers);
        }

        IBuffer[] buffers = new IBuffer[model.Parts.Count];
        for (int index = 0; index < buffers.Length; index++)
        {
            GpuMeshPart part = model.Parts[index];
            if (part.Skin is null)
            {
                buffers[index] = part.Vertices;
                continue;
            }

            BufferDescription description = new(part.Vertices.Description.Size, BufferUsage.ShaderRead | BufferUsage.ShaderWrite) { Name = "Posed vertices" };
            buffers[index] = _device.CreateBuffer(description);
        }

        _posed[entity] = (model, buffers);
        return buffers;
    }

    private void ReleaseUnseen()
    {
        List<int> gone = [.. _posed.Keys.Where(entity => !_seen.Contains(entity))];
        foreach (int entity in gone)
        {
            (GpuModel model, IBuffer[] buffers) = _posed[entity];
            DisposePosed(model, buffers);
            _posed.Remove(entity);
        }
    }

    // Only the buffers the skinner made, not the model's own; the device releases them once the GPU is done with them.
    private static void DisposePosed(GpuModel model, IBuffer[] buffers)
    {
        for (int index = 0; index < buffers.Length; index++)
        {
            if (!ReferenceEquals(buffers[index], model.Parts[index].Vertices))
            {
                buffers[index].Dispose();
            }
        }
    }

    private IBuffer GetJointBuffer(int slot, int size)
    {
        IBuffer? joints = _joints[slot];
        if (joints is not null && joints.Description.Size >= size)
        {
            return joints;
        }

        joints?.Dispose();
        long capacity = (long)BitOperations.RoundUpToPowerOf2((uint)size);
        BufferDescription description = new(capacity, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = $"Joints {slot}" };
        joints = _device.CreateBuffer(description);
        _joints[slot] = joints;
        return joints;
    }
}

