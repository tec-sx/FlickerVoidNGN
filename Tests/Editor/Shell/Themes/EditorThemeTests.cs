using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace FlickerVoid.Editor.Tests.Themes;

/// <summary>Checks the editor theme against rules R13 and R16 of Docs/Design/EditorUi.md.</summary>
public sealed partial class EditorThemeTests
{
    private static string ShellFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FlickerVoidNGN.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "Source", "Editor", "Shell");
    }

    private static Dictionary<string, string> Brushes()
    {
        var text = File.ReadAllText(Path.Combine(ShellFolder(), "Themes", "EditorTheme.axaml"));
        var brushes = new Dictionary<string, string>();
        foreach (Match match in BrushPattern().Matches(text))
        {
            brushes[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return brushes;
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var value = int.Parse(hex.AsSpan(1 + offset, 2), NumberStyles.HexNumber) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(0)) + (0.7152 * Channel(2)) + (0.0722 * Channel(4));
    }

    private static double Contrast(string a, string b)
    {
        var x = Luminance(a);
        var y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    [Fact]
    public void Text_reads_on_every_surface()
    {
        var brushes = Brushes();
        foreach (var surface in new[] { "EditorDeepBrush", "EditorPanelBrush", "EditorFrameBrush", "EditorControlBrush" })
        {
            Contrast(brushes["EditorTextBrush"], brushes[surface]).Should().BeGreaterThanOrEqualTo(7, surface);
            Contrast(brushes["EditorMutedTextBrush"], brushes[surface]).Should().BeGreaterThanOrEqualTo(4.5, surface);
        }
    }

    [Fact]
    public void The_accent_is_readable_and_carries_dark_text()
    {
        var brushes = Brushes();
        Contrast(brushes["EditorAccentBrush"], brushes["EditorPanelBrush"]).Should().BeGreaterThanOrEqualTo(3);
        Contrast("#211201", brushes["EditorAccentBrush"]).Should().BeGreaterThanOrEqualTo(4.5);
    }

    [Fact]
    public void Surfaces_stay_warm()
    {
        var brushes = Brushes();
        foreach (var surface in new[] { "EditorDeepBrush", "EditorPanelBrush", "EditorFrameBrush", "EditorControlBrush" })
        {
            var hex = brushes[surface];
            var red = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber);
            var green = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber);
            var blue = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber);
            (red > green && green > blue).Should().BeTrue($"{surface} is {hex}");
        }
    }

    [Fact]
    public void Views_other_than_the_theme_use_only_colours_from_the_theme()
    {
        var theme = File.ReadAllText(Path.Combine(ShellFolder(), "Themes", "EditorTheme.axaml")).ToUpperInvariant();
        var known = HexPattern().Matches(theme).Select(match => match.Value).ToHashSet();
        var stray = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ShellFolder(), "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".axaml") || f.EndsWith(".cs")))
        {
            if (file.EndsWith("EditorTheme.axaml") || file.Contains(Path.Combine("Shell", "obj")) || file.Contains(Path.Combine("Shell", "bin")))
            {
                continue;
            }

            foreach (Match match in HexPattern().Matches(File.ReadAllText(file).ToUpperInvariant()))
            {
                if (!known.Contains(match.Value))
                {
                    stray.Add($"{Path.GetFileName(file)} {match.Value}");
                }
            }
        }

        stray.Should().BeEmpty("rule R13: colours come from the theme");
    }

    [Fact]
    public void Views_build_their_sections_from_panel_cards()
    {
        var stray = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ShellFolder(), "*.axaml", SearchOption.AllDirectories))
        {
            if (file.EndsWith("EditorTheme.axaml") || file.Contains(Path.Combine("Shell", "obj")) || file.Contains(Path.Combine("Shell", "bin")))
            {
                continue;
            }

            foreach (Match match in SectionPattern().Matches(File.ReadAllText(file)))
            {
                stray.Add($"{Path.GetFileName(file)} {match.Value}");
            }
        }

        stray.Should().BeEmpty("rule R26: every section of a panel is a PanelCard, its fields PanelRows");
    }

    [Fact]
    public void Only_the_kit_defines_rulers_transports_and_faders()
    {
        var controls = Path.Combine(ShellFolder(), "Controls");
        var stray = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ShellFolder(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.StartsWith(controls) || file.Contains(Path.Combine("Shell", "obj")) || file.Contains(Path.Combine("Shell", "bin")))
            {
                continue;
            }

            foreach (Match match in KitControlPattern().Matches(File.ReadAllText(file)))
            {
                stray.Add($"{Path.GetFileName(file)} {match.Groups[1].Value}");
            }
        }

        stray.Should().BeEmpty("rule R28: everything with time uses the one timeline kit in Controls");
    }

    [GeneratedRegex(@"class (\w*(?:Ruler|Transport|Fader|Meter|TimelineStrip))\b")]
    private static partial Regex KitControlPattern();

    [GeneratedRegex(@"<Expander\b|Classes=""[^""]*\bsection(Body)?\b[^""]*""|\bfieldValue\b|SharedSizeGroup=""FieldLabel""")]
    private static partial Regex SectionPattern();

    [GeneratedRegex(@"<SolidColorBrush x:Key=""(\w+)"" Color=""(#[0-9A-Fa-f]{6})""")]
    private static partial Regex BrushPattern();

    [GeneratedRegex(@"#[0-9A-F]{6}(?:[0-9A-F]{2})?\b")]
    private static partial Regex HexPattern();
}
