using System.Reflection;
using System.Text.Json;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

public sealed class MoodValuesTests
{
    private readonly MoodValuesTestsBuilder _builder = new();

    [Fact]
    public void ListsEveryValueOfAMoodOnce()
    {
        IEnumerable<string> names = MoodValues.All.Select(value => value.Name);

        names.Should().Equal(GetValueNames(), "the table follows the mood's own order, which is the order files are written in");
    }

    [Fact]
    public void CopiesTheValueItNamesAndNoOther()
    {
        LightingMood changed = _builder.BuildChanged();

        foreach (MoodValue value in MoodValues.All)
        {
            LightingMood mood = new();

            value.Copy(changed, mood);

            GetChangedNames(mood).Should().Equal([value.Name], $"{value.Name} reads and writes its own value");
        }
    }

    [Fact]
    public void FindsAValueByItsName()
    {
        MoodValue? fog = MoodValues.Find("fogDensity");
        MoodValue? missing = MoodValues.Find("fogDensty");

        fog.Should().BeOfType<MoodNumber>();
        fog.Group.Should().Be(MoodGroup.Fog);
        fog.Owner.Should().Be(MoodOwner.Environment);
        missing.Should().BeNull();
    }

    [Fact]
    public void GivesTheLensTheGlowAndTheFinishAndTheEnvironmentTheRest()
    {
        IEnumerable<string> lens = MoodValues.All.Where(value => value.Owner == MoodOwner.Lens).Select(value => value.Name);

        lens.Should().Equal("bloom", "tonemapper", "whitePoint", "canvas");
    }

    [Fact]
    public void ReadsAndWritesANumberOfAMood()
    {
        MoodNumber exposure = MoodValues.Find("exposure").Should().BeOfType<MoodNumber>().Subject;
        LightingMood mood = new();

        exposure.Set(mood, 1.4f);

        mood.Exposure.Should().Be(1.4f);
        exposure.Get(mood).Should().Be(1.4f);
    }

    /// <summary>The mood's values as its files spell them: every property but the schema a file may name.</summary>
    private static IEnumerable<string> GetValueNames()
    {
        return GetValueProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name));
    }

    private static IEnumerable<PropertyInfo> GetValueProperties()
    {
        PropertyInfo[] properties = typeof(LightingMood).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return properties.Where(property => property.CanWrite && property.Name != nameof(LightingMood.Schema));
    }

    private static IEnumerable<string> GetChangedNames(LightingMood mood)
    {
        LightingMood untouched = new();
        foreach (PropertyInfo property in GetValueProperties())
        {
            object? value = property.GetValue(mood);
            object? original = property.GetValue(untouched);
            if (!Equals(value, original))
            {
                yield return JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            }
        }
    }
}
