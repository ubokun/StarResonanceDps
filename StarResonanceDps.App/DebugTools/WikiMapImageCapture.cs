// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Views.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.DebugTools;

/// <summary>
/// 開いているマップの窓を、Wiki の画像(4言語)として撮る。Debug の構成でだけビルドされる(入口はメイン窓の Ctrl+Shift+M)。
///
/// <para>
/// 撮っている間はパケットの処理を止め(処理のスレッドに待つ命令を積む。届いたパケットは待ち行列に残り、撮った後に処理される)、
/// 位置と周りの一覧を4言語で同じ値にする。窓は既定の大きさにし、プレイヤー名は「非表示」の表示(Wiki の画像の決まり)にして、
/// 言語を順に切り替えて撮る。撮った後に言語・プレイヤー名の表示・窓の大きさを元に戻し、処理を再開する。設定のファイルは書かない。
/// </para>
///
/// <para>
/// 画像は窓の中身を、Wiki の画像の道具(<c>tools\WikiImageRender</c>)と同じ背景と余白に重ねる。
/// 保存先は <c>Data/WikiImages/&lt;言語の印&gt;/Map&lt;番号&gt;.png</c> で、番号はまだどの言語にも無い一番小さい番号(押すたびに次の番号)。
/// 窓の見た目(テーマ・不透明度・ピン留めでヘッダーを隠すか)は窓の今の設定のまま撮る。
/// 結果はログに書き、成功は Asterisk・失敗は Hand の音で知らせる。
/// </para>
/// </summary>
internal static class WikiMapImageCapture
{
    private const string FileStem = "Map";
    private const int Padding = 24;

    /// <summary>処理のスレッドが止まるのを待つ長さ。前に積まれたパケットを処理し終えてから止まる。</summary>
    private static readonly TimeSpan PauseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>処理のスレッドを止めておく長さの上限。撮り終える前にこれを過ぎると、処理は再開する。</summary>
    private static readonly TimeSpan HoldTimeout = TimeSpan.FromSeconds(30);

    /// <summary>言語を切り替えた後、窓のレイアウトと地図の画像の当て直しが落ち着くまで待つ長さ。</summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(400);

    private static readonly (string Folder, int LanguageIndex, string CultureName)[] Languages =
    [
        ("ja", LocalizationManager.JapaneseLanguageIndex, "ja-JP"),
        ("ko", LocalizationManager.KoreanLanguageIndex, "ko-KR"),
        ("zh-CN", LocalizationManager.ChineseLanguageIndex, "zh-CN"),
        ("en", LocalizationManager.EnglishLanguageIndex, "en-US")
    ];

    private static bool _isCapturing;

    public static async void Capture()
    {
        if (_isCapturing)
        {
            return;
        }

        var window = Application.Current.Windows
            .OfType<WidgetWindow>()
            .FirstOrDefault(candidate => candidate.Widget.Kind == WidgetKind.Map);
        if (window is null)
        {
            Log.Warning("Wiki map capture: the Map window is not open");
            SystemSounds.Hand.Play();
            return;
        }

        _isCapturing = true;
        var dispatcher = window.Dispatcher;

        // 処理のスレッドが撮り終えた後に待ちから戻るので、ここでは手放さない(待ち合わせの印は GC に任せる)。
        var paused = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        MessageManager.RunOnPacketThread(() =>
        {
            paused.Set();

            // キャプチャが動いていないと、呼んだ画面のスレッドでそのまま走る。そのときは止めない。
            if (!dispatcher.CheckAccess())
            {
                release.Wait(HoldTimeout);
            }
        });

        var originalNameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        var originalWidth = window.Width;
        var originalHeight = window.Height;
        try
        {
            if (!await Task.Run(() => paused.Wait(PauseTimeout)))
            {
                Log.Warning("Wiki map capture: the packet thread did not pause within {Timeout}", PauseTimeout);
                SystemSounds.Hand.Play();
                return;
            }

            var defaultWindow = WidgetConfigDefaults.Create(WidgetKind.Map).Window;
            window.Width = defaultWindow.Width ?? originalWidth;
            window.Height = defaultWindow.Height ?? originalHeight;
            PlayerRosterPresentationStore.Instance.SetNameDisplayMode(PlayerNameDisplayMode.Hide);

            var number = FindNextNumber();
            var currentCultureName = LocalizationManager.Instance.CurrentCulture.Name;

            // どの言語も言語を切り替えた直後に撮る(切り替えで窓が名前と印を当て直す)。今の言語は最後に撮る。
            var ordered = Languages
                .OrderBy(language => string.Equals(language.CultureName, currentCultureName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (var language in ordered)
            {
                LocalizationManager.Instance.ApplyLanguageIndex(language.LanguageIndex);
                await SettleAsync(dispatcher);
                var path = Path.Combine(CombatRuntimePaths.DataDirectory, "WikiImages", language.Folder, $"{FileStem}{number}.png");
                SavePng(ComposeOnBackground(RenderElement((FrameworkElement)window.Content)), path);
            }

            Log.Information("Wiki map capture: saved {Stem}{Number}.png for {Count} languages", FileStem, number, ordered.Length);
            SystemSounds.Asterisk.Play();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Wiki map capture failed");
            SystemSounds.Hand.Play();
        }
        finally
        {
            LocalizationManager.Instance.ApplyLanguageIndex(ConfigManager.Instance.CurrentSettings.LanguageIndex);
            PlayerRosterPresentationStore.Instance.SetNameDisplayMode(originalNameDisplayMode);
            window.Width = originalWidth;
            window.Height = originalHeight;
            release.Set();
            _isCapturing = false;
        }
    }

    private static async Task SettleAsync(Dispatcher dispatcher)
    {
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(SettleDelay);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static int FindNextNumber()
    {
        var number = 1;
        while (Languages.Any(language => File.Exists(Path.Combine(
                   CombatRuntimePaths.DataDirectory,
                   "WikiImages",
                   language.Folder,
                   $"{FileStem}{number}.png"))))
        {
            number++;
        }

        return number;
    }

    private static BitmapSource RenderElement(FrameworkElement element)
    {
        var width = (int)Math.Ceiling(element.ActualWidth);
        var height = (int)Math.Ceiling(element.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>ウィジェットの後ろに敷く色(Wiki の画像の道具と同じ、ゲームの画面の代わり)。</summary>
    private static BitmapSource ComposeOnBackground(BitmapSource image)
    {
        var width = image.PixelWidth + (Padding * 2);
        var height = image.PixelHeight + (Padding * 2);
        var background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x3B, 0x4A, 0x56), 0),
                new GradientStop(Color.FromRgb(0x5A, 0x4A, 0x3E), 0.6),
                new GradientStop(Color.FromRgb(0x2E, 0x3A, 0x33), 1)
            }
        };

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(background, null, new Rect(0, 0, width, height));
            context.DrawImage(image, new Rect(Padding, Padding, image.PixelWidth, image.PixelHeight));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void SavePng(BitmapSource image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
