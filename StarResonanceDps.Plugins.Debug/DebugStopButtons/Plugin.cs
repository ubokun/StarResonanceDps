// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugStopButtons.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 押して止める")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugStopButtons;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    // 周りの差分と出現の通知。数が多いので次の受信が早い。
    private static readonly PluginPacketType[] BusyMessageTypes =
    [
        new PluginPacketType(PluginPacketKind.Notify, ServiceId: 1664308034, MethodId: 0x2D),
        new PluginPacketType(PluginPacketKind.Notify, ServiceId: 1664308034, MethodId: 0x6)
    ];

    private IPluginContext? _context;
    private IAsyncEnumerator<PluginPacketMessage>? _unreadMessages;

    public void Initialize(IPluginContext context)
    {
        _context = context;
    }

    public FrameworkElement CreateContent()
    {
        var panel = new StackPanel { Margin = new Thickness(24, 8, 24, 24) };
        panel.Children.Add(CreateButton("画面のスレッドで例外(止まる)", ThrowOnUiThread));
        panel.Children.Add(CreateButton("待たれない Task の例外(止まる)", ThrowInUnobservedTask));
        panel.Children.Add(CreateButton("自分のスレッドの例外(アプリが落ちる)", ThrowOnOwnThread));
        panel.Children.Add(CreateButton("取り残しを上限まで進める(次の受信で止まる)", FillPendingToLimit));
        panel.Children.Add(CreateButton("通信を取らずに溜める(実際に 64MB まで)", StartLeavingMessagesUnread));
        return panel;
    }

    public void Shutdown()
    {
        _context = null;
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Content = text, Width = 300, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
        button.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Buttons.Text);
        button.Click += (_, _) => action();
        return button;
    }

    private static void ThrowOnUiThread()
    {
        throw new InvalidOperationException("Debug exception on the UI thread.");
    }

    private static void ThrowInUnobservedTask()
    {
        StartFaultedTask();

        // 例外が届くのは Task が回収されたとき。
        _ = Task.Delay(500).ContinueWith(_ =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }, TaskScheduler.Default);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void StartFaultedTask()
    {
        _ = Task.Run(() => throw new InvalidOperationException("Debug exception in an unobserved task."));
    }

    private static void ThrowOnOwnThread()
    {
        new Thread(() => throw new InvalidOperationException("Debug exception on the plugin's own thread.")) { IsBackground = true }.Start();
    }

    /// <summary>読み始めて、その先を読まない。</summary>
    private async void StartLeavingMessagesUnread()
    {
        if (_context is null || _unreadMessages is not null)
        {
            return;
        }

        _context.Logger.Info("Started leaving game messages unread");
        _unreadMessages = _context.Packets.ReadAsync(BusyMessageTypes).GetAsyncEnumerator();
        await _unreadMessages.MoveNextAsync();
    }

    /// <summary>読み始めて先を読まず、取り残しの勘定をちょうど上限にする。次に届いたメッセージで上限を越える。</summary>
    private async void FillPendingToLimit()
    {
        if (_context is null || _unreadMessages is not null)
        {
            return;
        }

        _unreadMessages = _context.Packets.ReadAsync(BusyMessageTypes).GetAsyncEnumerator();
        var firstMessage = _unreadMessages.MoveNextAsync();

        // 本体の内部を直に触る(確かめ用のプラグインだけ)。
        var account = _context.Packets;
        var limit = (long)account.GetType().DeclaringType!.GetField("MaxPendingBytesPerPlugin")!.GetRawConstantValue()!;
        account.GetType().GetMethod("AddPending")!.Invoke(account, [(int)limit]);

        await firstMessage;
    }
}
