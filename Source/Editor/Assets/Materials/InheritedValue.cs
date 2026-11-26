using System.Text.Json;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>A parameter's value a material keeps from a parent, and the content path of the parent that sets it.</summary>
public sealed record InheritedValue(JsonElement Value, string Source);
