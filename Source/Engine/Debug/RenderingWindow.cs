using System.Numerics;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;

using Hexa.NET.ImGui;

namespace FlickerVoid.Engine.Debug;

/// <summary>
/// Switches the scene renderer's features (lights, probes, ray tracing, DLSS) and shows the size it draws at, shows the G-buffer's
/// targets in place of the frame and tweaks its lighting mood live: the sun, ambient light, shadows, fog, screen-space shadows,
/// bloom and grade.
/// </summary>
public sealed class RenderingWindow : DebugWindow
{
    private static readonly string[] ViewNames = ["Lit", "Albedo", "Normal", "Material", "Object depth", "Motion", "Depth"];
    private static readonly string[] UpscalingNames = ["Off", "DLSS Quality", "DLAA"];

    private readonly SceneRenderer _renderer;
    private readonly LightingMood _initial;

    public RenderingWindow(SceneRenderer renderer)
        : base("Rendering", "Rendering")
    {
        _renderer = renderer;
        _initial = renderer.Mood with { };
    }

    public override void Render()
    {
        RenderStatistics statistics = _renderer.Statistics;
        ImGui.Text($"Mesh parts drawn {statistics.Drawn}, culled {statistics.Culled}");
        ImGui.Text($"In shadows drawn {statistics.ShadowDrawn}, culled {statistics.ShadowCulled}");
        bool localLights = _renderer.LocalLights;
        if (ImGui.Checkbox("Point and spot lights", ref localLights))
        {
            _renderer.LocalLights = localLights;
        }

        bool probes = _renderer.Probes;
        if (ImGui.Checkbox("Irradiance probes", ref probes))
        {
            _renderer.Probes = probes;
        }

        bool rayTracing = _renderer.RayTracing;
        if (ImGui.Checkbox("Ray tracing (where the GPU has ray queries)", ref rayTracing))
        {
            _renderer.RayTracing = rayTracing;
        }

        bool liveProbes = _renderer.LiveProbes;
        if (ImGui.Checkbox("Live probes (with ray tracing)", ref liveProbes))
        {
            _renderer.LiveProbes = liveProbes;
        }

        RenderUpscaling(statistics);
        int view = (int)_renderer.DebugView;
        if (ImGui.Combo("View", ref view, ViewNames, ViewNames.Length))
        {
            _renderer.DebugView = (DebugView)view;
        }

        LightingMood mood = _renderer.Mood;
        if (ImGui.Button("Reset to the mood it started with"))
        {
            _renderer.Mood = _initial with { };
            mood = _renderer.Mood;
        }

        if (ImGui.CollapsingHeader("Sun and ambient", ImGuiTreeNodeFlags.DefaultOpen))
        {
            Vector3 direction = mood.SunDirection;
            if (ImGui.DragFloat3("Sun direction", ref direction, 0.01f))
            {
                mood.SunDirection = direction.LengthSquared() > 1e-6f ? Vector3.Normalize(direction) : mood.SunDirection;
            }

            float ambient = mood.Ambient;
            if (ImGui.SliderFloat("Ambient", ref ambient, 0, 1))
            {
                mood.Ambient = ambient;
            }

            float shadowDistance = mood.SunShadowDistance;
            if (ImGui.SliderFloat("Sun shadow distance (0 is off)", ref shadowDistance, 0, 100))
            {
                mood.SunShadowDistance = shadowDistance;
            }

            float sunAngle = mood.SunAngle;
            if (ImGui.SliderFloat("Sun angle (softens traced shadows)", ref sunAngle, 0, 30))
            {
                mood.SunAngle = sunAngle;
            }
        }

        if (ImGui.CollapsingHeader("Fog"))
        {
            float density = mood.FogDensity;
            if (ImGui.SliderFloat("Density", ref density, 0, 1, "%.3f", ImGuiSliderFlags.Logarithmic))
            {
                mood.FogDensity = density;
            }

            Vector3 color = mood.FogColor;
            if (ImGui.ColorEdit3("Color", ref color))
            {
                mood.FogColor = color;
            }

            float anisotropy = mood.FogAnisotropy;
            if (ImGui.SliderFloat("Anisotropy", ref anisotropy, -1, 1))
            {
                mood.FogAnisotropy = anisotropy;
            }

            float height = mood.FogHeight;
            if (ImGui.DragFloat("Height", ref height, 0.05f))
            {
                mood.FogHeight = height;
            }

            float falloff = mood.FogHeightFalloff;
            if (ImGui.SliderFloat("Height falloff (0 is even)", ref falloff, 0, 5))
            {
                mood.FogHeightFalloff = falloff;
            }
        }

        if (ImGui.CollapsingHeader("Screen-space shadows"))
        {
            float occlusion = mood.AmbientOcclusion;
            if (ImGui.SliderFloat("Ambient occlusion", ref occlusion, 0, 1))
            {
                mood.AmbientOcclusion = occlusion;
            }

            float radius = mood.AmbientOcclusionRadius;
            if (ImGui.SliderFloat("Occlusion radius", ref radius, 0.05f, 3))
            {
                mood.AmbientOcclusionRadius = radius;
            }

            float contact = mood.ContactShadowLength;
            if (ImGui.SliderFloat("Contact shadow length (0 is off)", ref contact, 0, 2))
            {
                mood.ContactShadowLength = contact;
            }
        }

        if (ImGui.CollapsingHeader("Post-processing"))
        {
            float bloom = mood.Bloom;
            if (ImGui.SliderFloat("Bloom", ref bloom, 0, 1))
            {
                mood.Bloom = bloom;
            }

            float exposure = mood.Exposure;
            if (ImGui.SliderFloat("Exposure", ref exposure, 0.1f, 4))
            {
                mood.Exposure = exposure;
            }

            float contrast = mood.Contrast;
            if (ImGui.SliderFloat("Contrast", ref contrast, 0.5f, 2))
            {
                mood.Contrast = contrast;
            }

            float saturation = mood.Saturation;
            if (ImGui.SliderFloat("Saturation", ref saturation, 0, 2))
            {
                mood.Saturation = saturation;
            }

            Vector3 tint = mood.Tint;
            if (ImGui.ColorEdit3("Tint", ref tint))
            {
                mood.Tint = tint;
            }
        }
    }

    private void RenderUpscaling(RenderStatistics statistics)
    {
        int upscaling = (int)_renderer.Upscaling;
        if (ImGui.Combo("Upscaling", ref upscaling, UpscalingNames, UpscalingNames.Length))
        {
            _renderer.Upscaling = (UpscaleMode)upscaling;
        }

        string drawnAt = $"Drawn at {statistics.RenderWidth}x{statistics.RenderHeight} for {statistics.OutputWidth}x{statistics.OutputHeight}";
        if (statistics.Upscaled)
        {
            ImGui.Text($"{drawnAt}, upscaled by DLSS");
        }
        else if (_renderer.Device.Features.HasFlag(GraphicsFeatures.Dlss))
        {
            ImGui.Text(drawnAt);
        }
        else
        {
            ImGui.Text($"{drawnAt}; no DLSS on this GPU");
        }
    }
}
