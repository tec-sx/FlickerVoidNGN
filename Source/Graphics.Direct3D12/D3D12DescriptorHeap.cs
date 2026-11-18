using Silk.NET.Direct3D12;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>A descriptor heap with slots handed out one at a time; slot 0 stays unused.</summary>
internal sealed unsafe class D3D12DescriptorHeap : IDisposable
{
    private readonly uint _increment;
    private readonly CpuDescriptorHandle _cpuStart;
    private readonly GpuDescriptorHandle _gpuStart;

    public D3D12DescriptorHeap(ID3D12Device4* device, DescriptorHeapType type, int capacity, bool shaderVisible)
    {
        DescriptorHeapDesc description = new()
        {
            Type = type,
            NumDescriptors = (uint)capacity,
            Flags = DescriptorHeapFlags.None,
        };
        if (shaderVisible)
        {
            description.Flags = DescriptorHeapFlags.ShaderVisible;
        }

        Guid heapId = ID3D12DescriptorHeap.Guid;
        ID3D12DescriptorHeap* heap = null;
        D3D12Com.Check(device->CreateDescriptorHeap(&description, &heapId, (void**)&heap));
        Heap = heap;
        _increment = device->GetDescriptorHandleIncrementSize(type);
        _cpuStart = heap->GetCPUDescriptorHandleForHeapStart();
        if (shaderVisible)
        {
            _gpuStart = heap->GetGPUDescriptorHandleForHeapStart();
        }

        Slots = new BindlessIndexAllocator(capacity);
    }

    public ID3D12DescriptorHeap* Heap { get; }

    public BindlessIndexAllocator Slots { get; }

    public GpuDescriptorHandle GpuStart => _gpuStart;

    public CpuDescriptorHandle GetCpuHandle(uint slot) => new() { Ptr = _cpuStart.Ptr + slot * _increment };

    public void Dispose()
    {
        D3D12Com.Release(Heap);
    }
}
