// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 蘇生リストの1行(戦闘不能の人1人)。行は作り直さず、値だけ入れ替える(<see cref="ViewModels.ReviveListWidgetViewModel"/>)。
/// 行の形とゲージはデバフ一覧の行と同じ。クラスアイコン・名前の作りはプレイヤーリストの行と同じ。
/// </summary>
public sealed partial class ReviveListEntry : ObservableObject
{
    public ReviveListEntry(long characterId)
    {
        CharacterId = characterId;
    }

    public long CharacterId { get; }

    /// <summary>自分の行か。右クリックのメニューの「ステータス詳細」は自分の行だけに出す(プレイヤーリストと同じ)。</summary>
    [ObservableProperty]
    private bool _isSelf;

    /// <summary>この行の右クリックのメニューを開いているか。開いている間は行のホバーの背景を残す。下ろすとメニューを閉じる(画面のコード)。</summary>
    [ObservableProperty]
    private bool _isPlayerSelectionMenuOpen;

    /// <summary>クラスアイコンの形。鍵(職業)ごとに使い回す。</summary>
    [ObservableProperty]
    private Brush? _classIconMask;

    /// <summary>クラスアイコンの色(設定「アイコンカラー」)。色ごとに使い回す。</summary>
    [ObservableProperty]
    private Brush? _classBrush;

    /// <summary>名前。書式は設定「プレイヤー名」(プレイヤーリストと同じ)。</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>自分の行を強調表示するか。設定「自分の強調表示」がオンのときの自分の行だけ true。</summary>
    [ObservableProperty]
    private bool _isSelfHighlighted;

    /// <summary>右寄せの文字。蘇生不可の残り秒、蘇生可能、分からなければ「??秒」。</summary>
    [ObservableProperty]
    private string _stateText = string.Empty;

    /// <summary>蘇生可能か。右の文字をテキストカラー「蘇生可能」の色にする。</summary>
    [ObservableProperty]
    private bool _isRevivable;

    /// <summary>テキストカラー「蘇生可能」の色。全部の行で同じブラシ。</summary>
    [ObservableProperty]
    private Brush? _revivableBrush;

    /// <summary>ゲージの長さ(0〜1)。右に残り秒を出している行だけ長さを持ち、ほかの行は 0。</summary>
    [ObservableProperty]
    private double _barRatio;

    /// <summary>
    /// ゲージが 0 になるまでの秒数。表示側はこのぶんかけて線形に縮める。
    /// 0 は「今は縮まない」(満タンで頭打ちの間と、ゲージを出さない行)。
    /// </summary>
    [ObservableProperty]
    private double _barDecaySeconds;

    /// <summary>並びの鍵。蘇生可能は最初、残り秒の分かる行は蘇生不可デバフの切れる時刻、分からない行は最後。</summary>
    internal DateTime SortKey { get; set; }
}
