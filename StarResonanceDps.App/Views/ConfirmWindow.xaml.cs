// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
    }

    public static bool Show(Window owner, string titleResourceKey, string messageResourceKey, string detailResourceKey)
    {
        var localization = LocalizationManager.Instance;
        return ShowText(
            owner,
            localization.GetString(titleResourceKey),
            localization.GetString(messageResourceKey),
            localization.GetString(detailResourceKey));
    }

    public static bool ShowText(Window owner, string title, string message, string detail)
    {
        var window = new ConfirmWindow
        {
            Owner = owner
        };

        window.HeaderText.Text = title;
        window.MessageText.Text = message;
        window.DetailText.Text = detail;

        // ShowDialog はアプリのウィンドウを全部止めるので、ウィジェットも操作できなくなる。止めるのはオーナーだけにする。
        OwnerModalWindow.ShowAndWait(window, owner);
        return window.IsConfirmed;
    }

    /// <summary>
    /// 「はい」で閉じたか。<see cref="Window.DialogResult"/> は <see cref="Window.ShowDialog"/> で
    /// 表示した窓にしか設定できないため、オーナーだけを止める表示に合わせて自前で持つ。
    /// </summary>
    public bool IsConfirmed { get; private set; }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close();
    }
}
