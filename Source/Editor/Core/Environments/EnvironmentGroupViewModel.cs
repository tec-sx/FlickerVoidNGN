namespace FlickerVoid.Editor.Core.Environments;

/// <summary>A heading of the environment editor's values, such as Sun or Fog, and the values under it.</summary>
public sealed class EnvironmentGroupViewModel(string name, IReadOnlyList<EnvironmentValueViewModel> values)
{
    public string Name { get; } = name;

    public IReadOnlyList<EnvironmentValueViewModel> Values { get; } = values;
}
