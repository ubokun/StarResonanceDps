// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using CommunityToolkit.Mvvm.Input;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// バフ・デバフカードの窓の ViewModel。プレイヤーのカードとモンスターのカードの2種類があり、
/// 同じビュー(<see cref="Views.Widgets.BuffDebuffCardWidgetView"/>)の右クリックのメニューが使う。項目は出せるものだけを出し、1つも無ければ開かない。
/// </summary>
public interface IBuffDebuffCardMenuViewModel
{
    /// <summary>メニューの色を引くウィジェット。</summary>
    WidgetListItemViewModel Widget { get; }

    /// <summary>メニューの1つ目の項目「グループに追加」。</summary>
    string AddToGroupMenuText { get; }

    /// <summary>カードのバフをバフ・デバフグループに足せるか。偽ならメニューに項目を出さない。</summary>
    bool CanAddToGroup();

    IRelayCommand AddToGroupCommand { get; }

    /// <summary>メニューの2つ目の項目「バフ・デバフカードのショートカットに追加」。</summary>
    string AddToShortcutsMenuText { get; }

    /// <summary>カードのバフをバフ・デバフカードのショートカットに足せるか。偽ならメニューに項目を出さない。</summary>
    bool CanAddToShortcuts();

    IRelayCommand AddToShortcutsCommand { get; }

    /// <summary>メニューの3つ目の項目「このバフを通知対象にする」「〜しない」(デバフのカードは「このデバフを」)。今の通知対象の状態で決まる。</summary>
    string NotificationTargetMenuText { get; }

    /// <summary>カードのバフを通知対象にできるか(しないにできるか)。追うバフが決まっていれば真。偽ならメニューに項目を出さない。</summary>
    bool CanToggleNotificationTarget();

    /// <summary>カードのバフを、カードの設定の通知対象に入れる・外す(今の状態の逆)。</summary>
    IRelayCommand ToggleNotificationTargetCommand { get; }

    /// <summary>メニューを開く直前に、文言を今の状態にする(通知対象かどうかは設定の窓でも変わる)。</summary>
    void RefreshMenuText();
}
