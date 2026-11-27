using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>Three numbers, X, Y and Z, such as a shot's place; each change hands the whole vector to the row's action.</summary>
public sealed partial class VectorRow : ToolRow
{
    private Action<Vector3> _set;
    private bool _taking;

    public VectorRow(string label, Vector3 value, Action<Vector3> set)
        : base(label)
    {
        _set = set;
        _taking = true;
        X = value.X;
        Y = value.Y;
        Z = value.Z;
        _taking = false;
    }

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    public partial double Z { get; set; }

    public override void Take(ToolRow other)
    {
        VectorRow row = (VectorRow)other;
        _set = row._set;
        _taking = true;
        X = row.X;
        Y = row.Y;
        Z = row.Z;
        _taking = false;
    }

    partial void OnXChanged(double value) => Send();

    partial void OnYChanged(double value) => Send();

    partial void OnZChanged(double value) => Send();

    private void Send()
    {
        if (!_taking)
        {
            _set(new Vector3((float)X, (float)Y, (float)Z));
        }
    }
}
