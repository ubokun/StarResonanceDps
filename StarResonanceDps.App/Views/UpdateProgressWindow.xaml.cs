// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 更新の進み具合の窓。今のバージョンの取得と展開、更新のモードの待ちと置き換えで使う。
/// 閉じるのは処理の側(<see cref="CloseNow"/>)。閉じるボタンと Alt+F4 はキャンセルの依頼にする。
/// </summary>
public partial class UpdateProgressWindow : Window
{
    private bool _canClose;

    private UpdateProgressWindow()
    {
        InitializeComponent();
    }

    /// <summary>キャンセルが押された。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>持ち主が無ければ(更新のモード)、画面の中央に出してタスクバーにも出す。</summary>
    public static UpdateProgressWindow Create(Window? owner)
    {
        return new UpdateProgressWindow
        {
            Owner = owner,
            ShowInTaskbar = owner is null,
            WindowStartupLocation = owner is null
                ? System.Windows.WindowStartupLocation.CenterScreen
                : System.Windows.WindowStartupLocation.CenterOwner
        };
    }

    /// <summary>今の段の文を出す。バーと詳細は、次に <see cref="ShowProgress"/> を呼ぶまで隠す。</summary>
    public void ShowStatus(string resourceKey)
    {
        StatusText.Text = LocalizationManager.Instance.GetString(resourceKey);
        ProgressGauge.Visibility = Visibility.Collapsed;
        DetailText.Visibility = Visibility.Collapsed;
    }

    /// <param name="ratio">進み具合(0〜1)。</param>
    /// <param name="detail">バーの下の文。無ければ出さない。</param>
    public void ShowProgress(double ratio, string? detail = null)
    {
        ProgressGauge.Value = ratio;
        ProgressGauge.Visibility = Visibility.Visible;
        DetailText.Text = detail ?? string.Empty;
        DetailText.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>ここから先は取り消せない。</summary>
    public void DisableCancel()
    {
        CancelButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
    }

    public void CloseNow()
    {
        _canClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_canClose)
        {
            e.Cancel = true;
            if (CancelButton.IsEnabled)
            {
                CancelRequested?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        base.OnClosing(e);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
