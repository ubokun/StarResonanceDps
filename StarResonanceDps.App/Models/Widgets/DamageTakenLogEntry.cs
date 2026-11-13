// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 被ダメログの1行(被弾1回)。作りはメーターの行と同じで、左に時刻(タイムラインの行と同じ)・クラスアイコン(設定「アイコンカラー」で染める)・名前、
/// 右に被弾(スキル名・被ダメ値・属性アイコン)、下敷きに右から伸びるバー。一覧は上が新しい(<see cref="ViewModels.DamageTakenLogWidgetViewModel"/>)。
/// 当て直すのは時刻・名前とクラス(名簿)・文字(設定・言語)・バー(最大が変わったとき)と、設定「最新の被ダメのみ表示」で同じ人が被弾したときの被弾。
/// </summary>
public sealed partial class DamageTakenLogEntry : ObservableObject
{
    public DamageTakenLogEntry(DamageTakenLogHit hit)
    {
        Hit = hit;
    }

    /// <summary>この行の被弾。設定「最新の被ダメのみ表示」では、同じ人の新しい被弾に入れ替わる(人は変わらない)。</summary>
    public DamageTakenLogHit Hit { get; set; }

    /// <summary>被弾した人のキャラ番号。右クリックのメニューと左クリックが開く窓(被ダメ詳細)の相手。</summary>
    public long CharacterId => Hit.CharacterId;

    /// <summary>この行の右クリックのメニューを開いているか。開いている間は行のホバーの背景を残す。下ろすとメニューを閉じる(画面のコード)。</summary>
    [ObservableProperty]
    private bool _isPlayerSelectionMenuOpen;

    /// <summary>時刻(戦闘の経過「00:00:00」、タイムラインの行と同じ)。戦闘の時計の起点がまだ無ければ空欄。</summary>
    [ObservableProperty]
    private string _timeText = string.Empty;

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

    /// <summary>右の書式(設定「スキル名」)の文字。</summary>
    [ObservableProperty]
    private string _hitInfoText = string.Empty;

    /// <summary>被ダメ値(タイムラインの回復・ダメージの行と同じ数字の書き方)。</summary>
    [ObservableProperty]
    private string _valueText = string.Empty;

    /// <summary>被ダメ値の後ろに赤い「(戦闘不能)」を出すか。その被弾で倒れた、かつ設定「戦闘不能の原因を表示」がオン。</summary>
    [ObservableProperty]
    private bool _isDefeatShown;

    /// <summary>
    /// 被弾の属性が分かるか。属性を保存していなかった頃の記録は分からないので、属性アイコン・バー・TIPS を出さない(スキル詳細と同じ)。
    /// </summary>
    [ObservableProperty]
    private bool _hasElement;

    /// <summary>属性アイコンの形。鍵(属性)ごとに使い回す。塗りは白。</summary>
    [ObservableProperty]
    private Brush? _elementIconMask;

    /// <summary>行の TIPS(属性名「火属性」)。</summary>
    [ObservableProperty]
    private string _elementToolTipText = string.Empty;

    /// <summary>バーの塗り(設定「属性カラー」のその属性の色に、不透明度を掛けたもの)。属性ごとに使い回す。</summary>
    [ObservableProperty]
    private Brush? _barBrush;

    /// <summary>バーの長さ(0〜1)。一覧の中で被ダメ値が一番大きい被弾を 1 にする。</summary>
    [ObservableProperty]
    private double _barRatio;
}
