// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 知らせの窓を出し、閉じるまで待つ。
    /// 窓の移動・大きさの変更の最中に呼ばれたときは、終わってから出し、呼び出し元は待たずに戻る
    /// (<see cref="OwnerModalWindow.RunWhenNotMovingOrSizing"/>)。その間にオーナーが閉じていたら、オーナーなしで画面の中央に出す。
    /// </summary>
    public static void Show(Window? owner, string title, string message)
    {
        Show(owner, title, message, (string?)null);
    }

    /// <inheritdoc cref="Show(Window, string, string)"/>
    public static void Show(Window? owner, string title, string message, string? detail)
    {
        ShowWhenNotMovingOrSizing(owner, title, message, window =>
        {
            window.DetailText.Text = detail ?? string.Empty;
            window.DetailText.Visibility = string.IsNullOrWhiteSpace(detail)
                ? Visibility.Collapsed
                : Visibility.Visible;
        });
    }

    /// <summary>
    /// 詳細の欄にリンクを含めて出す。リンクは <c>Hyperlink.Inline</c> で描き、押したら開く。
    /// 出す時機とオーナーの扱いは <see cref="Show(Window, string, string)"/> と同じ。
    /// </summary>
    public static void Show(Window? owner, string title, string message, IReadOnlyList<MessageDetailPart> detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        // 後から出すことがあるので、呼び出し元の一覧はこの時点の中身を写して使う。
        var parts = detail.ToArray();
        ShowWhenNotMovingOrSizing(owner, title, message, window =>
        {
            var linkStyle = (Style)window.FindResource("Hyperlink.Inline");
            foreach (var part in parts)
            {
                if (part.Url is null)
                {
                    AddText(window.DetailText.Inlines, part.Text);
                    continue;
                }

                var url = part.Url;
                var link = new Hyperlink { Style = linkStyle };
                AddText(link.Inlines, part.Text);
                link.Click += (_, _) => ExternalLinkOpener.Open(url);
                window.DetailText.Inlines.Add(link);
            }

            window.DetailText.Visibility = parts.Any(part => !string.IsNullOrWhiteSpace(part.Text))
                ? Visibility.Visible
                : Visibility.Collapsed;
        });
    }

    private static void ShowWhenNotMovingOrSizing(Window? owner, string title, string message, Action<MessageWindow> fillDetail)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        // 出すのを待っている間にオーナーが閉じたら、オーナーなしで出す(閉じた窓はオーナーにできない。知らせは落とさない)。
        var isOwnerClosed = false;
        EventHandler onOwnerClosed = (_, _) => isOwnerClosed = true;
        if (owner is not null)
        {
            owner.Closed += onOwnerClosed;
        }

        OwnerModalWindow.RunWhenNotMovingOrSizing(() =>
        {
            if (owner is not null)
            {
                owner.Closed -= onOwnerClosed;
            }

            var actualOwner = isOwnerClosed ? null : owner;
            var window = Create(actualOwner, title, message);
            fillDetail(window);

            // ShowDialog はアプリのウィンドウを全部止めるので、ウィジェットも操作できなくなる。止めるのはオーナーだけにする。
            OwnerModalWindow.ShowAndWait(window, actualOwner);
        });
    }

    private static MessageWindow Create(Window? owner, string title, string message)
    {
        var window = new MessageWindow
        {
            Owner = owner,
            WindowStartupLocation = owner is null
                ? System.Windows.WindowStartupLocation.CenterScreen
                : System.Windows.WindowStartupLocation.CenterOwner
        };

        window.HeaderText.Text = title;
        window.MessageText.Text = message;
        return window;
    }

    /// <summary>改行を <see cref="LineBreak"/> にして足す。</summary>
    private static void AddText(InlineCollection inlines, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            if (lines[i].Length > 0)
            {
                inlines.Add(new Run(lines[i]));
            }
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // DialogResult は ShowDialog で表示した窓にしか設定できないので、閉じるだけにする(結果は使わない)。
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
