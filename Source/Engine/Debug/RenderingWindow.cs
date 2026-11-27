using System.Numerics;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Debugging;

using Hexa.NET.ImGui;

namespace FlickerVoid.Engine.Debug;

/// <summary>
/// Switches the scene renderer's features (lights, probes, ray tracing, DLSS; the <c>r.</c> console variables) and shows the size it draws at, shows the G-buffer's
/// targets in place of the frame and tweaks its lighting mood live: the sun, ambient light, shadows, fog, screen-space shadows,
/// bloom and grade.
/// </summary>
public sealed class RenderingWindow : DebugWindow
{
    private static readonly string[] ViewNames = ["Lit", "Albedo", "Normal", "Material", "Object depth", "Motion", "Depth"];
    private static readonly string[] UpscalingNames = ["Off", "DLSS Quality", "DLAA"];

    private readonly SceneRenderer _renderer;
    private readonly RenderingSettings _settings;
    private readonly LightingMood _initial;

    /// <summary>Switches the renderer through the game's settings, so the console shows what it switched, or through settings of its own without them.</summary>
    public RenderingWindow(SceneRenderer renderer, RenderingSettings? settings = null)
        : base("Rendering", "Rendering")
    {
        _renderer = renderer;
        _settings = settings ?? RenderingSettings.Capture(renderer);
        _initial = renderer.Mood with { };
    }

    public override void Render()
    {
        RenderStatistics statistics = _renderer.Statistics;
        ImGui.Text($"Mesh parts drawn {statistics.Drawn}, culled {statistics.Culled}");
        ImGui.Text($"In shadows drawn {statistics.ShadowDrawn}, culled {statistics.ShadowCulled}");
        ImGui.Text($"Shadow tiles drawn {statistics.ShadowTilesDrawn}, kept {statistics.ShadowTilesKept}");
        ImGui.Text($"Batches drawn {statistics.BatchesDrawn}, culled {statistics.BatchesCulled}; copies sent {statistics.CopiesSubmitted}, drawn {statistics.CopiesDrawn}");
        RenderSwitch(_settings.LocalLights, "Point and spot lights");
        RenderSwitch(_settings.Probes, "Irradiance probes");
        RenderSwitch(_settings.RayTracing, "Ray tracing (where the GPU has ray queries)");
        RenderSwitch(_settings.LiveProbes, "Live probes (with ray tracing)");
        RenderSwitch(_settings.VSync, "VSync (caps the frame rate at the refresh rate)");
        RenderUpscaling(statistics);
        int view = (int)_settings.DebugView.Value;
        if (ImGui.Combo("View", ref view, ViewNames, ViewNames.Length))
        {
            _settings.DebugView.Value = (DebugView)view;
            _settings.Apply(_renderer);
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

            float bloomRadius = mood.BloomRadius;
            if (ImGui.SliderFloat("Bloom radius", ref bloomRadius, 0, 1))
            {
                mood.BloomRadius = bloomRadius;
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
        int upscaling = (int)_settings.Upscaling.Value;
        if (ImGui.Combo("Upscaling", ref upscaling, UpscalingNames, UpscalingNames.Length))
        {
            _settings.Upscaling.Value = (UpscaleMode)upscaling;
            _settings.Apply(_renderer);
        }

        RenderSwitch(_settings.RayReconstruction, "Ray Reconstruction (with ray tracing and DLSS)");

        string drawnAt = $"Drawn at {statistics.RenderWidth}x{statistics.RenderHeight} for {statistics.OutputWidth}x{statistics.OutputHeight}";
        if (statistics.Denoised)
        {
            ImGui.Text($"{drawnAt}, denoised and upscaled by DLSS Ray Reconstruction");
        }
        else if (statistics.Upscaled)
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

    private void RenderSwitch(ConsoleVariable<bool> variable, string label)
    {
        bool on = variable.Value;
        if (ImGui.Checkbox(label, ref on))
        {
            variable.Value = on;
            _settings.Apply(_renderer);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(variable.Name);
        }
    }
}
