// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using Windows.UI.Notifications;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 通知の入口。通知タブのその項目の設定(スイッチ・出し方・音量・声)で、Windows の通知か読み上げに出す。スイッチがオフなら出さない。
/// 何を通知するかは判定の側が決め、文章(Windows の通知の本文・読み上げの内容)を渡す。
///
/// <para>
/// 失敗はメッセージを出さずログに残す(ゲーム中にエラーの窓を出さない)。同じ種類のログは <see cref="NotificationFailureLog"/> が間引く。
/// </para>
///
/// <para>
/// Windows の通知は、パッケージ化していないアプリなので、出す前にアプリの名前とアイコンを
/// <c>HKCU\Software\Classes\AppUserModelId\{登録名}</c> に書く。アイコンは埋め込みの png を
/// <see cref="AppDataPaths.WindowsNotificationIconPath"/> へ書き出したもの。
/// 登録名は <c>StarResonanceDps.</c> ＋ その png のパスの SHA-256。Windows は登録名ごとに最初に読んだアイコンの場所を覚えて
/// 読み直さないので、png の場所が変われば(フォルダの移動を含む)登録名も変わる形にしてある。
/// Windows の通知を使うオンの項目が無くなったら、通知センターのこのアプリの通知・登録・png を消す
/// (<see cref="RemoveWindowsNotificationRegistration"/>)。
/// </para>
///
/// <para>
/// Windows の通知の音は、トーストでは消し、アプリが通知音量を掛けて鳴らす(<see cref="SpeechQueue"/>)。
/// トーストの音には音量を指定する手段が無く、Windows の応答不可の間はトーストの音が鳴らないため。
/// 通知音量が 0 なら鳴らさず、読み上げもしない。
/// </para>
/// </summary>
public sealed class NotificationService : IDisposable
{
    private const string AppUserModelIdPrefix = "StarResonanceDps.";
    private const string AppUserModelIdRootKeyPath = @"Software\Classes\AppUserModelId";
    private const string IconResourceName = "Icon.png";

    private static readonly string IconPath = AppDataPaths.WindowsNotificationIconPath;
    private static readonly string AppUserModelId = CreateAppUserModelId(IconPath);
    private static readonly string AppUserModelIdKeyPath = AppUserModelIdRootKeyPath + @"\" + AppUserModelId;
    private static readonly Lazy<byte[]> EmbeddedIcon = new(ReadEmbeddedIcon);

    // 登録・古い登録の掃除・削除を1つずつ通す。
    private static readonly object RegistrationSync = new();
    private static bool _hasRemovedStaleRegistrations;

    private static readonly Lazy<NotificationService> LazyInstance = new(() => new NotificationService());

    private readonly SpeechQueue _speechQueue = new();

    private NotificationService()
    {
    }

    public static NotificationService Instance => LazyInstance.Value;

    /// <summary>アプリの終了。読み上げの順番待ちを止める(一度も使っていなければ何もしない)。</summary>
    public static void Shutdown()
    {
        if (LazyInstance.IsValueCreated)
        {
            LazyInstance.Value.Dispose();
        }
    }

    public void Notify(NotificationKind kind, string text)
    {
        Notify(ConfigManager.Instance.GetNotificationItem(kind), text);
    }

    /// <summary>項目の設定(本体の項目もプラグインの項目も)で出す。オフの項目と空の文は出さない。</summary>
    public void Notify(NotificationItemConfig item, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !item.IsEnabled)
        {
            return;
        }

        var hasVolume = item.Volume > AppConfigDefaults.NotificationVolumeMin;
        switch (item.MethodIndex)
        {
            case AppConfigDefaults.NotificationMethodWindowsIndex:
                ShowWindowsNotification(text);
                if (hasVolume)
                {
                    _speechQueue.Enqueue(new NotificationSoundRequest(item.Volume));
                }

                break;
            case AppConfigDefaults.NotificationMethodSpeechIndex:
                if (hasVolume)
                {
                    _speechQueue.Enqueue(new SpeechRequest(
                        text,
                        item.SpeechVoiceIndex,
                        item.VoicevoxStyleId,
                        LocalizationManager.Instance.CurrentCulture,
                        item.Volume));
                }

                break;
        }
    }

    public void Dispose()
    {
        _speechQueue.Dispose();
    }

    /// <summary>
    /// Windows の通知を使うオンの項目が無くなったときに呼ぶ。通知センターのこのアプリの通知、アプリの名前とアイコンの登録、
    /// 書き出した png を消す(無ければ何もしない)。
    /// </summary>
    public static void RemoveWindowsNotificationRegistration()
    {
        lock (RegistrationSync)
        {
            try
            {
                bool isRegistered;
                using (var key = Registry.CurrentUser.OpenSubKey(AppUserModelIdKeyPath))
                {
                    isRegistered = key is not null;
                }

                if (isRegistered)
                {
                    RemoveRegistration(AppUserModelId);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                Log.Warning(ex, "Could not remove the Windows notification registration key={Key}", AppUserModelIdKeyPath);
            }

            try
            {
                if (File.Exists(IconPath))
                {
                    File.Delete(IconPath);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                Log.Warning(ex, "Could not remove the Windows notification icon path={Path}", IconPath);
            }
        }
    }

    private static void ShowWindowsNotification(string text)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            NotificationFailureLog.Warning(
                "windows-notification-os",
                null,
                "Windows notifications need Windows 10 or later. The notification was not shown");
            return;
        }

        try
        {
            EnsureWindowsNotificationRegistration();
            ShowToast(text);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or SecurityException or IOException)
        {
            NotificationFailureLog.Warning("windows-notification", ex, "Could not show a Windows notification");
        }
    }

    /// <summary>
    /// アプリの名前(管理画面のタイトル)とアイコンを登録する。値が今と同じなら書かない。
    /// 起動してから最初の1回だけ、ほかの場所の古い登録を消す(<see cref="RemoveStaleRegistrations"/>)。
    /// png が書けなかった回もアイコンの場所は登録し、通知はアイコン無しで出る(失敗はログ)。後で書けたときに読まれる見込みのため。
    /// </summary>
    private static void EnsureWindowsNotificationRegistration()
    {
        lock (RegistrationSync)
        {
            if (!_hasRemovedStaleRegistrations)
            {
                _hasRemovedStaleRegistrations = true;
                RemoveStaleRegistrations();
            }

            WriteIconFile();

            var displayName = LocalizationManager.Instance.GetString("Window_Manager_Title");
            using var key = Registry.CurrentUser.CreateSubKey(AppUserModelIdKeyPath, writable: true);
            if (!string.Equals(key.GetValue("DisplayName") as string, displayName, StringComparison.Ordinal))
            {
                key.SetValue("DisplayName", displayName);
            }

            if (!string.Equals(key.GetValue("IconUri") as string, IconPath, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue("IconUri", IconPath);
            }
        }
    }

    /// <summary>埋め込みのアイコンを png に書き出す。無いか中身が違うときだけ書く。書けなければログに残す。</summary>
    private static void WriteIconFile()
    {
        try
        {
            var icon = EmbeddedIcon.Value;
            if (File.Exists(IconPath) && File.ReadAllBytes(IconPath).AsSpan().SequenceEqual(icon))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(IconPath)!);
            File.WriteAllBytes(IconPath, icon);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException)
        {
            NotificationFailureLog.Warning(
                "windows-notification-icon",
                ex,
                "Could not write the Windows notification icon. The notification is shown without the icon path={Path}",
                IconPath);
        }
    }

    /// <summary>
    /// フォルダを動かした・消した後に残った、このアプリのほかの登録を消す。
    /// 対象はアイコンのファイルが無いものだけで、そのドライブが今無いもの(外したドライブにあるコピー)は残す。
    /// </summary>
    private static void RemoveStaleRegistrations()
    {
        string[] names;
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(AppUserModelIdRootKeyPath);
            if (root is null)
            {
                return;
            }

            names = root.GetSubKeyNames();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            Log.Warning(ex, "Could not list the Windows notification registrations key={Key}", AppUserModelIdRootKeyPath);
            return;
        }

        foreach (var name in names)
        {
            if (!name.StartsWith(AppUserModelIdPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, AppUserModelId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                string? iconUri;
                using (var key = Registry.CurrentUser.OpenSubKey(AppUserModelIdRootKeyPath + @"\" + name))
                {
                    iconUri = key?.GetValue("IconUri") as string;
                }

                if (!IsStaleIcon(iconUri))
                {
                    continue;
                }

                RemoveRegistration(name);
                Log.Information("Removed a stale Windows notification registration name={Name} icon={Icon}", name, iconUri);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                Log.Warning(ex, "Could not remove a stale Windows notification registration name={Name}", name);
            }
        }
    }

    private static bool IsStaleIcon(string? iconUri)
    {
        if (string.IsNullOrEmpty(iconUri))
        {
            return false;
        }

        var root = Path.GetPathRoot(iconUri);
        return !string.IsNullOrEmpty(root) && Directory.Exists(root) && !File.Exists(iconUri);
    }

    /// <summary>登録名の通知を通知センターから消してから、登録の鍵を消す。通知センターから消せなくても鍵は消す(ログに残す)。</summary>
    private static void RemoveRegistration(string appUserModelId)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            try
            {
                ToastNotificationManager.History.Clear(appUserModelId);
            }
            catch (COMException ex)
            {
                Log.Warning(ex, "Could not clear the Windows notifications name={Name}", appUserModelId);
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree(AppUserModelIdRootKeyPath + @"\" + appUserModelId, throwOnMissingSubKey: false);
    }

    /// <summary>登録名。<c>StarResonanceDps.</c> ＋ アイコンの png のパス(大文字にそろえる)の SHA-256 の16進。</summary>
    private static string CreateAppUserModelId(string iconPath)
    {
        var normalizedPath = Path.GetFullPath(iconPath).ToUpperInvariant();
        return AppUserModelIdPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));
    }

    private static byte[] ReadEmbeddedIcon()
    {
        using var stream = typeof(NotificationService).Assembly.GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {IconResourceName} is missing");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    [SupportedOSPlatform("windows10.0.10240")]
    private static void ShowToast(string text)
    {
        var content = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);
        content.GetElementsByTagName("text")[0].AppendChild(content.CreateTextNode(text));

        // 音はアプリが鳴らすので、トーストの音は消す(消さないと応答不可でないときに二重に鳴る)。
        var audio = content.CreateElement("audio");
        audio.SetAttribute("silent", "true");
        content.DocumentElement.AppendChild(audio);

        ToastNotificationManager.CreateToastNotifier(AppUserModelId).Show(new ToastNotification(content));
    }
}

/// <summary>
/// 通知の失敗のログ。ゲーム中に失敗が続いてもログが埋まらないよう、同じ種類は最初の1件の後 1分に1件だけ書き、
/// その間に書かなかった件数を添える(パケット処理の例外のログと同じ決まり)。
/// </summary>
internal static class NotificationFailureLog
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, (DateTime LastLogged, int Suppressed)> State = new(StringComparer.Ordinal);

    public static void Warning(string kind, Exception? exception, string messageTemplate, params object?[] values)
    {
        int suppressed;
        lock (Sync)
        {
            var now = DateTime.Now;
            if (State.TryGetValue(kind, out var state))
            {
                if (now - state.LastLogged < Interval)
                {
                    State[kind] = (state.LastLogged, state.Suppressed + 1);
                    return;
                }

                suppressed = state.Suppressed;
            }
            else
            {
                suppressed = 0;
            }

            State[kind] = (now, 0);
        }

        Log.Warning(exception, messageTemplate + " (skipped {Suppressed} since the last log)", [.. values, suppressed]);
    }
}
