using Silk.NET.Direct3D12;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>The enhanced-barrier layout, sync scope and access each <see cref="ResourceState"/> stands for; buffers ignore the layout.</summary>
internal readonly record struct D3D12ResourceStates(BarrierLayout Layout, BarrierSync Sync, BarrierAccess Access)
{
    public static D3D12ResourceStates For(ResourceState state)
    {
        switch (state)
        {
            case ResourceState.Undefined:
                return new(BarrierLayout.Undefined, BarrierSync.None, BarrierAccess.NoAccess);
            case ResourceState.CopySource:
                return new(BarrierLayout.CopySource, BarrierSync.Copy, BarrierAccess.CopySource);
            case ResourceState.CopyDestination:
                return new(BarrierLayout.CopyDest, BarrierSync.Copy, BarrierAccess.CopyDest);
            case ResourceState.ShaderRead:
                return new(BarrierLayout.ShaderResource, BarrierSync.AllShading, BarrierAccess.ShaderResource);
            case ResourceState.RenderTarget:
                return new(BarrierLayout.RenderTarget, BarrierSync.RenderTarget, BarrierAccess.RenderTarget);
            case ResourceState.DepthWrite:
                return new(BarrierLayout.DepthStencilWrite, BarrierSync.DepthStencil, BarrierAccess.DepthStencilWrite);
            case ResourceState.DepthRead:
                return new(BarrierLayout.DepthStencilRead, BarrierSync.DepthStencil | BarrierSync.AllShading, BarrierAccess.DepthStencilRead | BarrierAccess.ShaderResource);
            case ResourceState.IndexBuffer:
                return new(BarrierLayout.Undefined, BarrierSync.IndexInput, BarrierAccess.IndexBuffer);
            case ResourceState.Present:
                return new(BarrierLayout.Present, BarrierSync.None, BarrierAccess.NoAccess);
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }
}
