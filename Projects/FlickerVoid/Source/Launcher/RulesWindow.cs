using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Attributes.Checks;
using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Debug;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Gameplay.World.Objects.Doors;

using Hexa.NET.ImGui;

namespace FlickerVoid.Launcher;

/// <summary>The protagonist's rules while the game interface is still to come: attributes with their modifiers, abilities, and the last lockpick check.</summary>
public sealed class RulesWindow : DebugWindow
{
    private readonly AttributeSystem _attributes;
    private readonly AbilitySystem _abilities;
    private readonly QuestSystem _quests;
    private LockpickedEvent? _lastPick;

    public RulesWindow(AttributeSystem attributes, AbilitySystem abilities, QuestSystem quests, EventBus events)
        : base("Rules", "Game")
    {
        _attributes = attributes;
        _abilities = abilities;
        _quests = quests;
        events.Handle((in LockpickedEvent picked) => _lastPick = picked);
    }

    public override void Render()
    {
        Vector2 size = new(420, 320);
        Vector2 position = new(20, 380);
        ImGui.SetWindowSize(size, ImGuiCond.FirstUseEver);
        ImGui.SetWindowPos(position, ImGuiCond.FirstUseEver);
        if (_quests.Player is not WorldEntityView player)
        {
            ImGui.TextDisabled("No protagonist is playing.");
            return;
        }

        ImGui.SeparatorText("Attributes");
        RenderAttributes(player.Entity);
        ImGui.SeparatorText("Modifiers");
        RenderModifiers(player.Entity);
        ImGui.SeparatorText("Abilities");
        RenderAbilities(player.Entity);
        ImGui.SeparatorText("Last check");
        RenderLastCheck();
    }

    private void RenderAttributes(Entity player)
    {
        IEnumerable<KeyValuePair<Tag, AttributeValue>> values = _attributes.GetValues(player).OrderBy(pair => pair.Value.Definition?.Category.Name).ThenBy(pair => pair.Key.Name);
        foreach ((Tag attribute, AttributeValue value) in values)
        {
            string name = value.Definition?.Display.Name ?? attribute.Name;
            ImGui.Text(name);
            ImGui.SameLine(150);
            if (value.Current == value.Base)
            {
                ImGui.Text($"{value.Current:0.##}");
                continue;
            }

            Vector4 colour = value.Current < value.Base ? new Vector4(0.95f, 0.5f, 0.4f, 1) : new Vector4(0.5f, 0.9f, 0.5f, 1);
            ImGui.TextColored(colour, $"{value.Current:0.##}");
            ImGui.SameLine();
            ImGui.TextDisabled($"(base {value.Base:0.##})");
        }
    }

    private void RenderModifiers(Entity player)
    {
        IReadOnlyList<AppliedModifier> modifiers = _attributes.GetModifiers(player);
        if (modifiers.Count == 0)
        {
            ImGui.TextDisabled("None.");
            return;
        }

        foreach (AppliedModifier applied in modifiers)
        {
            ModifierDefinition modifier = applied.Modifier;
            ImGui.BulletText(modifier.Display.Name);
            if (modifier.Display.ShortDescription.Length > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(modifier.Display.ShortDescription);
            }
        }
    }

    private void RenderAbilities(Entity player)
    {
        foreach (AbilitySpec spec in _abilities.GetAbilities(player))
        {
            bool can = _abilities.CanActivate(player, spec.Ability.Id, out Tag reason);
            string name = spec.Ability.Display.Name.Length > 0 ? spec.Ability.Display.Name : spec.Ability.Id.Name;
            ImGui.BulletText(name);
            ImGui.SameLine(150);
            if (spec.IsActive)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.5f, 1), "running");
            }
            else if (can)
            {
                ImGui.Text("ready");
            }
            else
            {
                string why = reason.Name[(reason.Name.LastIndexOf('.') + 1)..];
                ImGui.TextDisabled($"not now ({why})");
            }
        }
    }

    private void RenderLastCheck()
    {
        if (_lastPick is not { } picked)
        {
            ImGui.TextDisabled("Click the locked door with a lockpick and no key.");
            return;
        }

        CheckResult result = picked.Result;
        Vector4 colour = result.Success ? new Vector4(0.5f, 0.9f, 0.5f, 1) : new Vector4(0.95f, 0.5f, 0.4f, 1);
        string outcome = result.Success ? "Picked" : "Failed";
        ImGui.TextColored(colour, $"Lockpicking: {outcome}, {result.Total} against {result.Difficulty}");
        foreach (CheckLine line in result.Breakdown)
        {
            ImGui.BulletText($"{line.Label}");
            ImGui.SameLine(150);
            ImGui.Text($"{line.Value:+0;-0;0}");
        }
    }
}
