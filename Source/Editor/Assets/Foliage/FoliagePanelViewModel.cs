using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Engine.Foliage;

namespace FlickerVoid.Editor.Assets.Foliage;

/// <summary>
/// The foliage creator's panel: a tree or a bush, the seed it is grown from and the numbers of its shape, each choice growing the
/// plant anew in the file through the document.
/// </summary>
public sealed partial class FoliagePanelViewModel : CreatorPanelViewModel
{
    private readonly FoliageDocument _document;
    private readonly Random _random;
    private bool _showing;

    public FoliagePanelViewModel(FoliageDocument document, Random? random = null)
        : base(document)
    {
        _document = document;
        _random = random ?? Random.Shared;
        Show();
    }

    public override string Kind => "Foliage";

    public IReadOnlyList<FoliageKind> Types { get; } = [FoliageKind.Tree, FoliageKind.Bush];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTree))]
    public partial FoliageKind Type { get; set; }

    /// <summary>Whether the plant has a trunk and limbs to set.</summary>
    public bool IsTree => Type == FoliageKind.Tree;

    /// <summary>The number the plant's chances are drawn from.</summary>
    [ObservableProperty]
    public partial int Seed { get; set; }

    [ObservableProperty]
    public partial float Height { get; set; }

    [ObservableProperty]
    public partial float TrunkRadius { get; set; }

    [ObservableProperty]
    public partial int Limbs { get; set; }

    [ObservableProperty]
    public partial float CrownRadius { get; set; }

    [ObservableProperty]
    public partial int Crowns { get; set; }

    /// <summary>Grows another plant of the same shape, from a seed drawn by chance.</summary>
    [RelayCommand]
    public void NewSeed()
    {
        Seed = _random.Next(1, 100000);
    }

    partial void OnTypeChanged(FoliageKind value)
    {
        if (!_showing && value != _document.Plant.Kind)
        {
            _document.SetKind(value);
        }

        Show();
    }

    partial void OnSeedChanged(int value)
    {
        if (!_showing && value != _document.Plant.Seed)
        {
            _document.SetSeed(value);
        }

        Show();
    }

    partial void OnHeightChanged(float value) => WriteShape();

    partial void OnTrunkRadiusChanged(float value) => WriteShape();

    partial void OnLimbsChanged(int value) => WriteShape();

    partial void OnCrownRadiusChanged(float value) => WriteShape();

    partial void OnCrownsChanged(int value) => WriteShape();

    /// <summary>Grows the plant from the shape the panel shows; numbers nothing grows from are put back.</summary>
    private void WriteShape()
    {
        if (_showing)
        {
            return;
        }

        FoliageShape shape = new() { Height = Height, TrunkRadius = TrunkRadius, Limbs = Limbs, CrownRadius = CrownRadius, Crowns = Crowns };
        _document.SetShape(shape);
        Show();
    }

    private void Show()
    {
        if (_showing)
        {
            return;
        }

        _showing = true;
        FoliageFile plant = _document.Plant;
        Type = plant.Kind;
        Seed = plant.Seed;
        Height = plant.Shape.Height;
        TrunkRadius = plant.Shape.TrunkRadius;
        Limbs = plant.Shape.Limbs;
        CrownRadius = plant.Shape.CrownRadius;
        Crowns = plant.Shape.Crowns;
        ShowParts();
        _showing = false;
    }
}
