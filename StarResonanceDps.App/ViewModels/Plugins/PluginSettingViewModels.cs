using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.ViewModels.Plugins;

/// <summary>
/// プラグインが定義した設定の1行(通知タブの右下と、ウィジェットの設定の窓のプラグインの節)。行の作りは定義の種類ごとに画面が決め、
/// 値は文字で保存する(<see cref="PluginSettingValueCodec"/>)。
/// 値が変わったら <see cref="Changed"/>(文字の欄は <see cref="TextChanged"/>。通知タブは打ち終わってから保存する)で知らせる。読み込みでは上げない。
/// </summary>
public abstract partial class PluginSettingViewModel : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    /// <summary>行の下の注意書き。無ければ空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    private string _note = string.Empty;

    /// <summary>節の最後の行か(ウィジェットの設定の窓で、下の線を引かない)。</summary>
    [ObservableProperty]
    private bool _isLast;

    private protected PluginSettingViewModel(PluginSettingDefinition definition)
    {
        Definition = definition;
        RefreshLocalizedText();
    }

    public event EventHandler? Changed;

    public event EventHandler? TextChanged;

    public PluginSettingDefinition Definition { get; }

    public bool HasNote => Note.Length > 0;

    private protected bool IsLoading { get; private set; }

    public static PluginSettingViewModel Create(PluginSettingDefinition definition)
    {
        return definition switch
        {
            PluginSwitchSetting switchSetting => new PluginSwitchSettingViewModel(switchSetting),
            PluginChoiceSetting choiceSetting => new PluginChoiceSettingViewModel(choiceSetting),
            PluginNumberSetting numberSetting => new PluginNumberSettingViewModel(numberSetting),
            PluginTextSetting textSetting => new PluginTextSettingViewModel(textSetting),
            PluginSliderSetting sliderSetting => new PluginSliderSettingViewModel(sliderSetting),
            PluginColorSetting colorSetting => new PluginColorSettingViewModel(colorSetting),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.GetType().FullName, "Unsupported plugin setting type.")
        };
    }

    /// <summary>保存した値(無ければ null = 既定)を読む。</summary>
    public void Load(string? saved)
    {
        IsLoading = true;
        try
        {
            Apply(PluginSettingValueCodec.Read(Definition, saved));
        }
        finally
        {
            IsLoading = false;
        }
    }

    public string CreateSavedValue()
    {
        return PluginSettingValueCodec.Write(GetValue());
    }

    public virtual void RefreshLocalizedText()
    {
        var culture = LocalizationManager.Instance.CurrentCulture;
        Label = PluginTexts.Resolve(Definition.Names, culture) ?? string.Empty;
        Note = Definition.Notes is null ? string.Empty : PluginTexts.Resolve(Definition.Notes, culture) ?? string.Empty;
    }

    /// <summary>読んだ値(型は <see cref="PluginSettingValueCodec.Read"/>)を行に入れる。</summary>
    private protected abstract void Apply(object value);

    /// <summary>今の値(型は <see cref="PluginSettingValueCodec.Read"/> と同じ)。</summary>
    private protected abstract object GetValue();

    private protected void RaiseChanged()
    {
        if (!IsLoading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private protected void RaiseTextChanged()
    {
        if (!IsLoading)
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed partial class PluginSwitchSettingViewModel : PluginSettingViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private bool _isOn;

    internal PluginSwitchSettingViewModel(PluginSwitchSetting definition)
        : base(definition)
    {
    }

    /// <summary>スイッチの右に出す ON / OFF(ウィジェットの設定の窓。通知タブは出さない)。</summary>
    public string StateText => LocalizationManager.Instance.GetString(IsOn ? "Settings_Switch_On" : "Settings_Switch_Off");

    public override void RefreshLocalizedText()
    {
        base.RefreshLocalizedText();
        OnPropertyChanged(nameof(StateText));
    }

    private protected override void Apply(object value)
    {
        IsOn = (bool)value;
    }

    private protected override object GetValue()
    {
        return IsOn;
    }

    partial void OnIsOnChanged(bool value)
    {
        RaiseChanged();
    }
}

/// <summary>選択リストの選択肢。名前は言語を変えたときに入れ替える(選んでいる選択肢は変えない)。</summary>
public sealed partial class PluginChoiceOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    internal PluginChoiceOptionViewModel(PluginChoiceOption option)
    {
        Option = option;
    }

    internal PluginChoiceOption Option { get; }
}

public sealed partial class PluginChoiceSettingViewModel : PluginSettingViewModel
{
    [ObservableProperty]
    private PluginChoiceOptionViewModel? _selectedOption;

    internal PluginChoiceSettingViewModel(PluginChoiceSetting definition)
        : base(definition)
    {
        Options = [.. definition.Options.Select(option => new PluginChoiceOptionViewModel(option))];
        RefreshOptionNames();
    }

    public IReadOnlyList<PluginChoiceOptionViewModel> Options { get; }

    public override void RefreshLocalizedText()
    {
        base.RefreshLocalizedText();

        // 基のコンストラクタから呼ばれた時点では、選択肢はまだ無い。
        if (Options is not null)
        {
            RefreshOptionNames();
        }
    }

    private protected override void Apply(object value)
    {
        var optionId = ((PluginChoiceValue)value).OptionId;
        SelectedOption = Options.First(option => string.Equals(option.Option.Id, optionId, StringComparison.OrdinalIgnoreCase));
    }

    private protected override object GetValue()
    {
        return new PluginChoiceValue(SelectedOption!.Option.Id);
    }

    partial void OnSelectedOptionChanged(PluginChoiceOptionViewModel? value)
    {
        // 選択リストが null を書き戻したとき(項目を替えたときなど)は、値を変えない。
        if (value is not null)
        {
            RaiseChanged();
        }
    }

    private void RefreshOptionNames()
    {
        var culture = LocalizationManager.Instance.CurrentCulture;
        foreach (var option in Options)
        {
            option.DisplayName = PluginTexts.Resolve(option.Option.Names, culture) ?? string.Empty;
        }
    }
}

/// <summary>
/// 整数の欄。欄の文字は確定したとき(Enter・欄を離れたとき、画面のコード)だけ値にする。
/// 範囲の外は近いほうの端、空は打つ前の値に戻す(全体設定の計測時間の欄と同じ)。
/// </summary>
public sealed class PluginNumberSettingViewModel : PluginSettingViewModel
{
    private readonly PluginNumberSetting _definition;
    private long _value;

    internal PluginNumberSettingViewModel(PluginNumberSetting definition)
        : base(definition)
    {
        _definition = definition;
    }

    /// <summary>欄に出す文字(今の値)。欄の文字はバインドせず、画面が出すときと確定のたびに書き直す。</summary>
    public string DisplayText => _value.ToString(CultureInfo.InvariantCulture);

    /// <summary>欄の文字を確定する。数字として読めないときは値を変えずに false を返す。</summary>
    public bool TryCommitText(string text)
    {
        if (text.Length == 0 || !text.All(char.IsAsciiDigit))
        {
            return false;
        }

        // 桁が多すぎて long に入らない値は最大より大きいので、上の端にする。
        var number = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : long.MaxValue;
        var value = Math.Clamp(number, _definition.Minimum, _definition.Maximum);
        if (value != _value)
        {
            _value = value;
            RaiseChanged();
        }

        return true;
    }

    private protected override void Apply(object value)
    {
        _value = (long)value;
    }

    private protected override object GetValue()
    {
        return _value;
    }
}

public sealed partial class PluginTextSettingViewModel : PluginSettingViewModel
{
    [ObservableProperty]
    private string _text = string.Empty;

    internal PluginTextSettingViewModel(PluginTextSetting definition)
        : base(definition)
    {
    }

    private protected override void Apply(object value)
    {
        Text = (string)value;
    }

    private protected override object GetValue()
    {
        return Text;
    }

    partial void OnTextChanged(string value)
    {
        RaiseTextChanged();
    }
}

/// <summary>
/// スライドバー。「通知」では、値を受けるのはつまみを手放したときと、ドラッグ以外の操作のときだけ(通知音量と同じ、画面のコード)。
/// ウィジェットの設定の窓では、動かしている間も受ける(保存前のプレビュー)。
/// </summary>
public sealed partial class PluginSliderSettingViewModel : PluginSettingViewModel
{
    private readonly PluginSliderSetting _definition;

    [ObservableProperty]
    private double _value;

    internal PluginSliderSettingViewModel(PluginSliderSetting definition)
        : base(definition)
    {
        _definition = definition;
    }

    public double Minimum => _definition.Minimum;

    public double Maximum => _definition.Maximum;

    public double Step => _definition.Step;

    private protected override void Apply(object value)
    {
        Value = (double)value;
    }

    private protected override object GetValue()
    {
        return Value;
    }

    partial void OnValueChanged(double value)
    {
        RaiseChanged();
    }
}

/// <summary>色。枠の色と選んでいる枠は、テーマのウィンドウの色と同じ形(<see cref="ColorPaletteViewModel"/>)。ウィジェットの設定だけ。</summary>
public sealed class PluginColorSettingViewModel : PluginSettingViewModel
{
    internal PluginColorSettingViewModel(PluginColorSetting definition)
        : base(definition)
    {
        Colors = new ColorPaletteViewModel(definition.DefaultColors, WidgetConfigDefaults.MaxPaletteColorCount);
        Colors.PaletteChanged += Colors_PaletteChanged;
    }

    public ColorPaletteViewModel Colors { get; }

    /// <summary>スウォッチの名前の組(同じ窓のほかの色の行と分ける)。</summary>
    public string ColorGroupName => "PluginSetting." + Definition.Id;

    /// <summary>カラーピッカーで選んだ色。枠にあれば選び、無ければ足す。</summary>
    public void ApplyPickedColor(Color color)
    {
        Colors.AddOrSelect(color);
    }

    private protected override void Apply(object value)
    {
        var palette = (PluginColorPaletteValue)value;
        Colors.Load(palette.Colors, palette.SelectedIndex);
    }

    private protected override object GetValue()
    {
        return new PluginColorPaletteValue(Colors.GetHexColors(), Colors.SelectedIndex);
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        RaiseChanged();
    }
}
