// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class ColorOptionViewModel : ObservableObject
{
    private readonly ColorPaletteViewModel _owner;

    public ColorOptionViewModel(ColorPaletteViewModel owner, Color color)
    {
        _owner = owner;
        Color = color;
        Brush = CreateBrush(color);
    }

    public Color Color { get; }

    public SolidColorBrush Brush { get; }

    public string Hex => ColorUtilities.ToHex(Color);

    [ObservableProperty]
    private bool _isSelected;

    public void Select()
    {
        _owner.Select(this);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
