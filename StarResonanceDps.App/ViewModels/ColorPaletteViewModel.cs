// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class ColorPaletteViewModel : ObservableObject
{
    private readonly int _maxColorCount;
    private readonly IReadOnlyList<string> _defaultHexColors;

    public ColorPaletteViewModel(IEnumerable<string> defaultHexColors, int maxColorCount)
    {
        _maxColorCount = Math.Max(1, maxColorCount);
        _defaultHexColors = defaultHexColors.ToArray();
        Load(_defaultHexColors, 0);
    }

    public event EventHandler? PaletteChanged;

    public ObservableCollection<ColorOptionViewModel> Colors { get; } = new();

    [ObservableProperty]
    private int _selectedIndex;

    /// <summary>
    /// 選んでいる枠を見せないか。立っている間はどの枠も選んでいない表示にする(選んでいる番号と色は保つ)。
    /// ウィジェット設定のウィンドウの色で、背景画像を選んでいる間に立てる。
    /// </summary>
    [ObservableProperty]
    private bool _isSelectionHidden;

    public Color SelectedColor => Colors.Count == 0
        ? System.Windows.Media.Colors.White
        : Colors[Math.Clamp(SelectedIndex, 0, Colors.Count - 1)].Color;

    public IReadOnlyList<string> GetHexColors()
    {
        return Colors.Select(color => color.Hex).ToArray();
    }

    public void Load(IEnumerable<string>? hexColors, int selectedIndex)
    {
        var normalized = NormalizeHexColors(hexColors);

        if (normalized.Count == 0)
        {
            normalized = NormalizeHexColors(_defaultHexColors);
        }

        if (normalized.Count == 0)
        {
            normalized.Add("#FFFFFF");
        }

        Colors.Clear();
        foreach (var hex in normalized.Take(_maxColorCount))
        {
            if (ColorUtilities.TryParseHex(hex, out var color))
            {
                Colors.Add(new ColorOptionViewModel(this, color));
            }
        }

        if (Colors.Count == 0)
        {
            Colors.Add(new ColorOptionViewModel(this, System.Windows.Media.Colors.White));
        }

        SelectIndex(Math.Clamp(selectedIndex, 0, Colors.Count - 1), false);
        RaisePaletteChanged();
    }

    public void LoadRecent(IEnumerable<string>? hexColors)
    {
        Colors.Clear();
        foreach (var hex in NormalizeHexColors(hexColors).Take(_maxColorCount))
        {
            if (ColorUtilities.TryParseHex(hex, out var color))
            {
                Colors.Add(new ColorOptionViewModel(this, color));
            }
        }

        if (Colors.Count > 0)
        {
            SelectIndex(0, false);
        }
        else
        {
            SelectedIndex = -1;
            OnPropertyChanged(nameof(SelectedColor));
        }

        RaisePaletteChanged();
    }

    public void AddRecent(Color color)
    {
        var existingIndex = IndexOf(color);
        if (existingIndex >= 0)
        {
            var existing = Colors[existingIndex];
            Colors.RemoveAt(existingIndex);
            Colors.Insert(0, existing);
            SelectIndex(0, false);
            RaisePaletteChanged();
            return;
        }

        while (Colors.Count >= _maxColorCount)
        {
            Colors.RemoveAt(Colors.Count - 1);
        }

        Colors.Insert(0, new ColorOptionViewModel(this, color));
        SelectIndex(0, false);
        RaisePaletteChanged();
    }

    public void AddOrSelect(Color color)
    {
        var existingIndex = IndexOf(color);
        if (existingIndex >= 0)
        {
            SelectIndex(existingIndex, true);
            return;
        }

        while (Colors.Count >= _maxColorCount)
        {
            Colors.RemoveAt(0);
        }

        Colors.Add(new ColorOptionViewModel(this, color));
        SelectIndex(Colors.Count - 1, false);
        RaisePaletteChanged();
    }

    public void Select(ColorOptionViewModel option)
    {
        var index = Colors.IndexOf(option);
        if (index < 0)
        {
            return;
        }

        SelectIndex(index, true);
    }

    partial void OnIsSelectionHiddenChanged(bool value)
    {
        UpdateSelectionMarks();
    }

    private void SelectIndex(int index, bool raiseChanged)
    {
        SelectedIndex = index;
        UpdateSelectionMarks();

        OnPropertyChanged(nameof(SelectedColor));

        if (raiseChanged)
        {
            RaisePaletteChanged();
        }
    }

    private void UpdateSelectionMarks()
    {
        for (var i = 0; i < Colors.Count; i++)
        {
            Colors[i].IsSelected = !IsSelectionHidden && i == SelectedIndex;
        }
    }

    private int IndexOf(Color color)
    {
        for (var i = 0; i < Colors.Count; i++)
        {
            var item = Colors[i].Color;
            if (item.R == color.R && item.G == color.G && item.B == color.B)
            {
                return i;
            }
        }

        return -1;
    }

    private void RaisePaletteChanged()
    {
        PaletteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<string> NormalizeHexColors(IEnumerable<string>? hexColors)
    {
        var result = new List<string>();
        if (hexColors is null)
        {
            return result;
        }

        foreach (var hex in hexColors)
        {
            if (!ColorUtilities.TryParseHex(hex, out var color))
            {
                continue;
            }

            var normalized = ColorUtilities.ToHex(color);
            if (result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(normalized);
        }

        return result;
    }
}
