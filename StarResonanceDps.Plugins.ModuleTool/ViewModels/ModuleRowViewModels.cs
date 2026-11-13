// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;

namespace StarResonanceDps.Plugins.ModuleTool.ViewModels;

/// <summary>選択リストの1行(段階)。</summary>
internal sealed record ModuleOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>効果を追加する選択リストの1行。</summary>
internal sealed record ModuleEffectOption(int EffectId, string Name, ImageSource Icon)
{
    public override string ToString() => Name;
}

/// <summary>品質の切り替え1つ。</summary>
internal sealed class ModuleQualityToggleViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _isSelected;
    private string _label;

    public ModuleQualityToggleViewModel(int quality, string label, bool isSelected, Action changed)
    {
        Quality = quality;
        _label = label;
        _isSelected = isSelected;
        _changed = changed;
    }

    public int Quality { get; }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _changed();
            }
        }
    }

    /// <summary>変化の知らせ(保存)を出さずに選択を入れる。設定をまとめて当てるとき。</summary>
    public void SetSelected(bool isSelected)
    {
        SetProperty(ref _isSelected, isSelected, nameof(IsSelected));
    }
}

/// <summary>優先する効果の行。</summary>
internal sealed class ModulePriorityEffectRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private string _name;
    private IReadOnlyList<ModuleOption<int>> _levelOptions;
    private ModuleOption<int> _selectedLevel;

    public ModulePriorityEffectRowViewModel(
        int effectId,
        string name,
        ImageSource icon,
        IReadOnlyList<ModuleOption<int>> levelOptions,
        int minLevel,
        Action changed,
        Action<ModulePriorityEffectRowViewModel> remove)
    {
        EffectId = effectId;
        Icon = icon;
        _name = name;
        _levelOptions = levelOptions;
        _selectedLevel = FindLevel(levelOptions, minLevel);
        _changed = changed;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public int EffectId { get; }

    public ImageSource Icon { get; }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public IReadOnlyList<ModuleOption<int>> LevelOptions
    {
        get => _levelOptions;
        private set => SetProperty(ref _levelOptions, value);
    }

    public ModuleOption<int> SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (value is not null && SetProperty(ref _selectedLevel, value))
            {
                _changed();
            }
        }
    }

    public int MinLevel => _selectedLevel.Value;

    public RelayCommand RemoveCommand { get; }

    /// <summary>表示言語が変わったとき。選んでいる段階は変えない。</summary>
    public void Relabel(string name, IReadOnlyList<ModuleOption<int>> levelOptions)
    {
        var minLevel = MinLevel;
        Name = name;
        LevelOptions = levelOptions;
        _selectedLevel = FindLevel(levelOptions, minLevel);
        OnPropertyChanged(nameof(SelectedLevel));
    }

    private static ModuleOption<int> FindLevel(IReadOnlyList<ModuleOption<int>> options, int level)
    {
        return options.FirstOrDefault(option => option.Value == level) ?? options[0];
    }
}

/// <summary>結果のカードの効果1つ。<see cref="LevelText"/>は段階の表示(「Lv5」)。</summary>
internal sealed record ModuleEffectChipViewModel(ImageSource Icon, string Name, int Links, int Level, string LevelText);

/// <summary>モジュールの効果1つ(絵とリンク数)。<see cref="TipText"/> はモジュールの TIPS の1行(効果名とリンク数)。</summary>
internal sealed record ModulePartViewModel(ImageSource Icon, int Links, string TipText)
{
    public string LinksText => $"+{Links}";
}

/// <summary>結果のカードのモジュール1つ。</summary>
internal sealed record ModuleTileViewModel(
    ImageSource QualityImage,
    ImageSource ModuleImage,
    string Name,
    bool IsEquipped,
    IReadOnlyList<ModulePartViewModel> Parts);

/// <summary>結果のカード1枚(装着中の組か、見つけた組)。</summary>
internal sealed record ModuleResultCardViewModel(
    bool IsEquippedCard,
    string Title,
    string FightValueLabel,
    string FightValueText,
    string? DeltaText,
    int Delta,
    string LinkTotalText,
    IReadOnlyList<ModuleEffectChipViewModel> Chips,
    IReadOnlyList<ModuleTileViewModel> Tiles)
{
    public bool HasDelta => DeltaText is not null;

    public bool IsIncrease => Delta > 0;

    public bool IsDecrease => Delta < 0;
}
