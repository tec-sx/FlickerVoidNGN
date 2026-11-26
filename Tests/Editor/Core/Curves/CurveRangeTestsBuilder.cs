using FlickerVoid.Editor.Core.Curves;

namespace FlickerVoid.Editor.Core.Tests.Curves;

/// <summary>Builds a range showing a day of 24 hours and values from 0 to 10 on a 240 by 100 pixel view: 10 pixels an hour and a unit.</summary>
internal sealed class CurveRangeTestsBuilder
{
    public CurveRange Build()
    {
        CurveRange range = new();
        range.Resize(240, 100);
        range.Frame(0, 24, 0, 10, margin: 0);
        return range;
    }
}
