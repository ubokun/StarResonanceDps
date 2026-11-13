// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// ステータス詳細のテキストカラーの1行。作りはプレイヤーリストのクラスカラー
/// (<see cref="MeterClassColorItemViewModel"/>)と同じで、左のアイコンを<b>選んだ色で塗る</b>。
/// 項目と並びは <see cref="WidgetConfigDefaults.PlayerStatusTextColorKeys"/>: 先頭が値の増減の2行(アイコンは ▲ / ▼ の文字)、
/// 続いて「ステータス」の一覧(<c>PlayerStatusEntry.SettingRowAttrIds</c>)と同じ行。
/// </summary>
public sealed partial class PlayerStatusTextColorItemViewModel : ObservableObject
{
    private readonly string _nameKey;

    public PlayerStatusTextColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        _isLast = isLast;

        switch (key)
        {
            case WidgetConfigDefaults.PlayerStatusValueIncreaseTextColorKey:
                Mark = "▲";
                _nameKey = "Settings_PlayerStatus_TextColor_ValueIncrease";
                break;
            case WidgetConfigDefaults.PlayerStatusValueDecreaseTextColorKey:
                Mark = "▼";
                _nameKey = "Settings_PlayerStatus_TextColor_ValueDecrease";
                break;
            default:
                var attrId = int.Parse(key, NumberStyles.None, CultureInfo.InvariantCulture);
                _nameKey = PlayerStatusEntry.GetRowNameKey(attrId);
                IconMask = PlayerStatusEntry.GetRowIconMask(attrId);
                break;
        }
    }

    public ColorPaletteViewModel Colors { get; }

    /// <summary>最終行。区切り線を消して二重にならないようにする。並びが変わったら付け直す。</summary>
    [ObservableProperty]
    private bool _isLast;

    public string Key { get; }

    /// <summary>行のアイコンの形。選んだ色をこの形で抜く(クラスアイコンと同じ染め方)。増減の行は null。</summary>
    public Brush? IconMask { get; }

    /// <summary>増減の行のアイコンの文字(▲ / ▼)。選んだ色で書く。能力値の行は null。</summary>
    public string? Mark { get; }

    public bool HasMark => Mark is not null;

    public string DisplayName => LocalizationManager.Instance.GetString(_nameKey);

    /// <summary>既定の色。保存値が無いときに入れる。</summary>
    public List<string> CreateDefaultColors()
    {
        return WidgetConfigDefaults.CreateDefaultPlayerStatusTextColors(Key);
    }

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
