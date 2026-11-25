using System.Numerics;

using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Upscaling;

/// <summary>
/// What a frame tells a denoising upscaler of the surface at each pixel, at the render size, so it can tell noise from detail:
/// the share of diffuse and of specular light the surface reflects (linear, not sRGB), and its world normal with its roughness
/// in alpha; where nothing was drawn, grey (0.5), black and zeros. And the frame's view, without the jitter: world to view and
/// view to clip, multiplying row vectors.
/// </summary>
public readonly record struct DenoiseGuides(ITexture DiffuseAlbedo, ITexture SpecularAlbedo, ITexture NormalRoughness, Matrix4x4 WorldToView, Matrix4x4 ViewToClip);
