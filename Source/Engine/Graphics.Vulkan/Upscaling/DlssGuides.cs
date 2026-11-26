using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>What Ray Reconstruction reads beside a frame (FvDlssGuides): the surfaces' guide images and the view without the jitter.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DlssGuides
{
    public DlssImage DiffuseAlbedo;
    public DlssImage SpecularAlbedo;
    public DlssImage NormalRoughness;
    public Matrix4x4 WorldToView;
    public Matrix4x4 ViewToClip;
}
