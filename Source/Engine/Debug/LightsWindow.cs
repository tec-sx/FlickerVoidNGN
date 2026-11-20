using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

using Hexa.NET.ImGui;

namespace FlickerVoid.Engine.Debug;

/// <summary>Lists the world's point and spot lights, each with its color, intensity, range, shadows and position to tweak.</summary>
public sealed class LightsWindow : DebugWindow
{
    private static readonly QueryDescription PointLights = new QueryDescription().WithAll<PointLight, Transform>();
    private static readonly QueryDescription SpotLights = new QueryDescription().WithAll<SpotLight, Transform>();

    private readonly World _world;

    public LightsWindow(World world)
        : base("Lights", "Rendering")
    {
        _world = world;
    }

    public override void Render()
    {
        int number = 0;
        _world.Query(in PointLights, (Entity entity, ref PointLight light, ref Transform transform) =>
        {
            number++;
            if (ImGui.TreeNode($"{GetName(entity, "Point light", number)}##{entity.Id}"))
            {
                Vector3 color = light.Color;
                if (ImGui.ColorEdit3("Color", ref color))
                {
                    light.Color = color;
                }

                float intensity = light.Intensity;
                if (ImGui.SliderFloat("Intensity", ref intensity, 0, 10))
                {
                    light.Intensity = intensity;
                }

                float range = light.Range;
                if (ImGui.SliderFloat("Range", ref range, 0.1f, 20))
                {
                    light.Range = range;
                }

                bool shadows = light.CastsShadows;
                if (ImGui.Checkbox("Casts shadows", ref shadows))
                {
                    light.CastsShadows = shadows;
                }

                Vector3 position = transform.Position;
                if (ImGui.DragFloat3("Position", ref position, 0.05f))
                {
                    transform.Position = position;
                }

                ImGui.TreePop();
            }
        });

        _world.Query(in SpotLights, (Entity entity, ref SpotLight light, ref Transform transform) =>
        {
            number++;
            if (ImGui.TreeNode($"{GetName(entity, "Spot light", number)}##{entity.Id}"))
            {
                Vector3 color = light.Color;
                if (ImGui.ColorEdit3("Color", ref color))
                {
                    light.Color = color;
                }

                float intensity = light.Intensity;
                if (ImGui.SliderFloat("Intensity", ref intensity, 0, 10))
                {
                    light.Intensity = intensity;
                }

                float range = light.Range;
                if (ImGui.SliderFloat("Range", ref range, 0.1f, 20))
                {
                    light.Range = range;
                }

                float outer = light.OuterConeAngle;
                if (ImGui.SliderFloat("Outer cone", ref outer, 1, 90))
                {
                    light.OuterConeAngle = outer;
                }

                float inner = light.InnerConeAngle;
                if (ImGui.SliderFloat("Inner cone", ref inner, 0, outer))
                {
                    light.InnerConeAngle = inner;
                }

                bool shadows = light.CastsShadows;
                if (ImGui.Checkbox("Casts shadows", ref shadows))
                {
                    light.CastsShadows = shadows;
                }

                Vector3 position = transform.Position;
                if (ImGui.DragFloat3("Position", ref position, 0.05f))
                {
                    transform.Position = position;
                }

                ImGui.TreePop();
            }
        });

        if (number == 0)
        {
            ImGui.TextDisabled("No lights in the world.");
        }
    }

    private string GetName(Entity entity, string kind, int number)
    {
        if (_world.TryGet(entity, out EntityName name))
        {
            return name.Value;
        }

        return $"{kind} {number}";
    }
}
