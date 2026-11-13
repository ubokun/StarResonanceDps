// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// バフ/デバフ一覧の窓のViewModel。プレイヤーから開いたものと実体から開いたものの2種類があり、
/// 同じビュー(<see cref="Views.Widgets.PlayerBuffListWidgetView"/>)の行の右クリックのメニューが使う。
/// </summary>
public interface IBuffListWindowViewModel
{
    /// <summary>メニューの色を引くウィジェット。</summary>
    WidgetListItemViewModel Widget { get; }

    /// <summary>メニューの1つ目の項目。バフ一覧なら「バフカード」、デバフ一覧なら「デバフカード」。</summary>
    string CardMenuText { get; }

    /// <summary>右クリックのメニューを開いた行。優先表示の項目の文言はこの行の今の状態で決める。</summary>
    PlayerBuffEntry? MenuEntry { get; set; }

    /// <summary>メニューを開く直前に、優先表示の項目の文言を今の状態にする(同じ行を開き直すと <see cref="MenuEntry"/> が変わらないため)。</summary>
    void RefreshMenuText();

    /// <summary>
    /// メニューの2つ目の項目「このバフを優先表示」「このバフの優先表示をやめる」(デバフ一覧は「このデバフを」「このデバフの」)。
    /// 開いた行の今の優先表示の状態で決まる。
    /// </summary>
    string PriorityMenuText { get; }

    /// <summary>行のバフを優先表示にする・やめる(ウィジェットの優先表示リストの行をオン/オフにする)。</summary>
    IRelayCommand<PlayerBuffEntry?> TogglePriorityCommand { get; }

    /// <summary>メニューの3つ目の項目。バフ一覧なら「このバフを非表示」、デバフ一覧なら「このデバフを非表示」。</summary>
    string HideMenuText { get; }

    /// <summary>行のバフ・デバフカードを開く。行の左クリックと同じ。</summary>
    IRelayCommand<PlayerBuffEntry?> OpenCardCommand { get; }

    /// <summary>行のバフをウィジェットの非表示の一覧に足す。</summary>
    IRelayCommand<PlayerBuffEntry?> HideBuffCommand { get; }

    /// <summary>メニューの4つ目の項目「グループに追加」。</summary>
    string AddToGroupMenuText { get; }

    /// <summary>
    /// 行のバフをバフ・デバフグループに足せるか。自分が対象の一覧だけで、グループにまだオンで入っていないとき真。
    /// 偽ならメニューに項目を出さない。
    /// </summary>
    bool CanAddToGroup(PlayerBuffEntry? entry);

    /// <summary>行のバフをバフ・デバフグループに足す(料理・薬剤はまとまりで)。</summary>
    IRelayCommand<PlayerBuffEntry?> AddToGroupCommand { get; }

    /// <summary>メニューの5つ目の項目「バフ・デバフカードのショートカットに追加」。</summary>
    string AddToShortcutsMenuText { get; }

    /// <summary>
    /// 行のバフをバフ・デバフカードのショートカットに足せるか。自分が対象の一覧だけで、ショートカットにまだオンで入っていないとき真。
    /// 偽ならメニューに項目を出さない。
    /// </summary>
    bool CanAddToShortcuts(PlayerBuffEntry? entry);

    /// <summary>行のバフをバフ・デバフカードのショートカットに足す(料理・薬剤はまとまりで)。</summary>
    IRelayCommand<PlayerBuffEntry?> AddToShortcutsCommand { get; }
}
