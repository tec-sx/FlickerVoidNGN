using System.Numerics;

using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.Shadows;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Turns the scene's lights into GPU lights and hands out their shadows: while the frame traces rays, the sun's and the first
/// <see cref="ShadowRayPass.LampSlots"/> shadow-casting lamps' are traced, and shadow atlas tiles go to the rest, the sun
/// first, then point and spot lights in scene order while tiles last; traced lights keep their tiles only for the fog, which
/// rays don't light.
/// </summary>
internal sealed class LightGatherPass : IRenderPass
{
    // How far beyond the view toward the sun shadow casters are still drawn, in world units.
    private const float SunCasterReach = 50;

    // Texels between a filtered shadow lookup and its tile's edge, so filtering never reads a neighbouring tile.
    private const float TileInset = 2;

    private readonly ShadowAtlas _atlas = new();

    public void Prepare(RenderContext context)
    {
        _atlas.Reset();
        LightingMood mood = context.Mood;
        if (mood.SunShadowDistance > 0 && (!context.RayTracing || NeedsFogShadows(context)))
        {
            Vector3 sunDirection = Vector3.Normalize(mood.SunDirection);
            Matrix4x4 sun = ShadowProjections.FitSun(context.Camera, context.View, sunDirection, mood.SunShadowDistance, SunCasterReach);
            context.SunShadow = AddShadow(context, sun, _atlas.Sun, orthographic: true);
        }

        if (!context.LocalLights)
        {
            return;
        }

        foreach (RenderPointLight point in context.Scene.PointLights)
        {
            AddPointLight(context, point);
        }

        foreach (RenderSpotLight spot in context.Scene.SpotLights)
        {
            AddSpotLight(context, spot);
        }
    }

    public void Execute(RenderContext context)
    {
    }

    public void Dispose()
    {
    }

    private void AddPointLight(RenderContext context, in RenderPointLight point)
    {
        PointLight light = point.Light;
        uint raySlot = TakeRaySlot(context, light.CastsShadows);
        uint shadow = GpuLight.None;
        bool mapped = raySlot == GpuLight.None || NeedsFogShadows(context);
        if (light.CastsShadows && mapped && _atlas.FreeTiles >= ShadowProjections.PointFaces)
        {
            shadow = (uint)context.Shadows.Count;
            for (int face = 0; face < ShadowProjections.PointFaces; face++)
            {
                _atlas.TryAllocate(out ShadowTile tile);
                Matrix4x4 faceView = ShadowProjections.PointFace(point.Position, face, light.Range);
                AddShadow(context, faceView, tile, orthographic: false);
            }
        }

        Vector3 color = light.Color * light.Intensity;
        context.Lights.Add(new GpuLight(point.Position, light.Range, color, -3, -Vector3.UnitZ, -2, Vector3.UnitX, GpuLight.None, shadow, light.SourceRadius, raySlot));
        context.LightBounds.Add(new LightBounds(point.Position, light.Range));
    }

    private void AddSpotLight(RenderContext context, in RenderSpotLight spot)
    {
        SpotLight light = spot.Light;
        uint raySlot = TakeRaySlot(context, light.CastsShadows);
        uint shadow = GpuLight.None;
        bool mapped = raySlot == GpuLight.None || NeedsFogShadows(context);
        if (light.CastsShadows && mapped && _atlas.TryAllocate(out ShadowTile tile))
        {
            Matrix4x4 spotView = ShadowProjections.Spot(spot.Position, spot.Rotation, light.OuterConeAngle, light.Range);
            shadow = AddShadow(context, spotView, tile, orthographic: false);
        }

        uint cookie = GpuLight.None;
        if (light.Cookie is not null)
        {
            ITexture texture = context.Textures.Get(light.Cookie);
            cookie = texture.BindlessIndex;
        }

        Vector3 color = light.Color * light.Intensity;
        Vector3 direction = Vector3.Transform(-Vector3.UnitZ, spot.Rotation);
        Vector3 right = Vector3.Transform(Vector3.UnitX, spot.Rotation);
        float cosOuter = MathF.Cos(float.DegreesToRadians(light.OuterConeAngle));
        float cosInner = MathF.Cos(float.DegreesToRadians(light.InnerConeAngle));
        context.Lights.Add(new GpuLight(spot.Position, light.Range, color, cosOuter, direction, cosInner, right, cookie, shadow, light.SourceRadius, raySlot));
        context.LightBounds.Add(new LightBounds(spot.Position, light.Range));
    }

    /// <summary>
    /// The next light's channel of the traced shadows (the sun's is 0) while the frame traces rays and lamp channels are free,
    /// else <see cref="GpuLight.None"/>.
    /// </summary>
    private static uint TakeRaySlot(RenderContext context, bool castsShadows)
    {
        if (!castsShadows || !context.RayTracing || context.RayShadowLights.Count >= ShadowRayPass.LampSlots)
        {
            return GpuLight.None;
        }

        context.RayShadowLights.Add((uint)context.Lights.Count);
        return (uint)context.RayShadowLights.Count;
    }

    /// <summary>Whether the fog marches through the air, where traced shadows don't reach, so it needs shadow maps.</summary>
    private static bool NeedsFogShadows(RenderContext context)
    {
        return context.Mood.FogDensity > 0;
    }

    private static uint AddShadow(RenderContext context, Matrix4x4 viewProjection, ShadowTile tile, bool orthographic)
    {
        context.ShadowViews.Add(new ShadowView(viewProjection, tile));
        Matrix4x4 toTile = ShadowProjections.ToAtlasTile(tile);
        Matrix4x4 worldToAtlas = viewProjection * toTile;
        float inset = TileInset / ShadowAtlas.Size;
        float start = (float)tile.X / ShadowAtlas.Size;
        float top = (float)tile.Y / ShadowAtlas.Size;
        float size = (float)tile.Size / ShadowAtlas.Size;
        Vector4 rect = new(start + inset, top + inset, start + size - inset, top + size - inset);

        // The projection's x scale is 1 / half the view's width (orthographic) or 1 / tan(half the field of view) (perspective).
        Vector3 xAxis = new(viewProjection.M11, viewProjection.M21, viewProjection.M31);
        float texel = 2 / (xAxis.Length() * tile.Size);
        GpuShadow shadow;
        if (orthographic)
        {
            shadow = new GpuShadow(worldToAtlas, rect, 0, texel);
        }
        else
        {
            shadow = new GpuShadow(worldToAtlas, rect, texel, 0);
        }

        context.Shadows.Add(shadow);
        return (uint)(context.Shadows.Count - 1);
    }
}
