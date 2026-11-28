using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Mix;

/// <summary>
/// A level meter: a bar on the base colour that rises with the loudest sample lately, green to 72% of its length and warming to
/// yellow by 92%, as the mixer of the designs draws it; across or upward.
/// </summary>
public sealed class LevelMeter : Control
{
    public const double Lowest = -48;

    public static readonly StyledProperty<double> LevelProperty = AvaloniaProperty.Register<LevelMeter, double>(nameof(Level));

    public static readonly StyledProperty<bool> VerticalProperty = AvaloniaProperty.Register<LevelMeter, bool>(nameof(Vertical));

    static LevelMeter()
    {
        AffectsRender<LevelMeter>(LevelProperty, VerticalProperty);
    }

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool Vertical
    {
        get => GetValue(VerticalProperty);
        set => SetValue(VerticalProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        Color ground = ThemeColors.Find(this, "EditorFrameBrush");
        context.DrawRectangle(new SolidColorBrush(ground), null, bounds, 1, 1);
        double decibels = DecibelScale.FromLevel(Level);
        double position = DecibelScale.ToPosition(decibels, Lowest, 0);
        if (position <= 0)
        {
            return;
        }

        Color green = ThemeColors.Find(this, "EditorMeterBrush");
        Color yellow = ThemeColors.Find(this, "EditorMeterWarnBrush");
        LinearGradientBrush fill = new()
        {
            GradientStops = { new GradientStop(green, 0), new GradientStop(green, 0.72), new GradientStop(yellow, 0.92) },
        };
        Rect lit;
        if (Vertical)
        {
            fill.StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative);
            fill.EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
            double height = bounds.Height * position;
            lit = new Rect(0, bounds.Height - height, bounds.Width, height);
        }
        else
        {
            fill.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
            fill.EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative);
            lit = new Rect(0, 0, bounds.Width * position, bounds.Height);
        }

        using DrawingContext.PushedState clip = context.PushClip(lit);
        context.DrawRectangle(fill, null, bounds, 1, 1);
    }
}
