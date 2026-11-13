// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// バフ・デバフを1件だけ大きく出すカード。
/// プレイヤー用とモンスター用の2つのViewModelが同じこのビューを使う。
///
/// <para>
/// 右クリックのメニュー(「グループに追加」「バフ・デバフカードのショートカットに追加」「このバフを通知対象にする」など)は、出せる項目だけを出し、
/// 1つも無ければ開かない(<see cref="IBuffDebuffCardMenuViewModel"/>。グループとショートカットは自分のカードだけ、通知対象はどのカードでも)。
/// 開き方はバフ・デバフ一覧の行のメニューと同じ。
/// </para>
/// </summary>
public partial class BuffDebuffCardWidgetView : UserControl
{
    private const string CardContextMenuKey = "BuffDebuffCard.ContextMenu";
    private const string SingleCornerRadiusKey = "Menu.WidgetWindowBuffCardContextMenuItem.SingleCornerRadius";
    private const string FirstCornerRadiusKey = "Menu.WidgetWindowBuffCardContextMenuItem.FirstCornerRadius";
    private const string LastCornerRadiusKey = "Menu.WidgetWindowBuffCardContextMenuItem.LastCornerRadius";

    public BuffDebuffCardWidgetView()
    {
        InitializeComponent();
    }

    private void Card_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement target
            || DataContext is not IBuffDebuffCardMenuViewModel viewModel
            || TryFindResource(CardContextMenuKey) is not ContextMenu menu)
        {
            return;
        }

        // 項目の並びは画面の作りと同じ(1つ目がグループ、2つ目がショートカット、3つ目が通知対象)。
        var menuItems = menu.Items.OfType<MenuItem>().ToArray();
        var shows = new[] { viewModel.CanAddToGroup(), viewModel.CanAddToShortcuts(), viewModel.CanToggleNotificationTarget() };
        if (!shows.Any(show => show))
        {
            return;
        }

        viewModel.RefreshMenuText();

        for (var index = 0; index < menuItems.Length; index++)
        {
            menuItems[index].Visibility = index < shows.Length && shows[index] ? Visibility.Visible : Visibility.Collapsed;
        }

        ApplyCornerRadii(menuItems.Where(menuItem => menuItem.Visibility == Visibility.Visible).ToArray());

        menu.DataContext = viewModel;
        menu.PlacementTarget = target;
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>角丸の型は、出している項目の並びで決める(1つなら四隅、2つ以上なら先頭の上と末尾の下)。</summary>
    private void ApplyCornerRadii(IReadOnlyList<MenuItem> visibleItems)
    {
        for (var index = 0; index < visibleItems.Count; index++)
        {
            var key = visibleItems.Count == 1
                ? SingleCornerRadiusKey
                : index == 0
                    ? FirstCornerRadiusKey
                    : index == visibleItems.Count - 1
                        ? LastCornerRadiusKey
                        : null;

            visibleItems[index].Tag = key is not null && TryFindResource(key) is CornerRadius radius
                ? radius
                : new CornerRadius(0);
        }
    }

    private void BuffDebuffCardWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (TryFindResource(CardContextMenuKey) is ContextMenu { IsOpen: true } menu)
        {
            menu.IsOpen = false;
        }
    }
}
