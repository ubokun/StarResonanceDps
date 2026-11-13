// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Microsoft.Win32;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 起動設定「Windows起動時にアプリを起動」。現在のユーザーの起動の登録(<c>Run</c>)に、このアプリの実行ファイルを書く・消す。
/// 管理者権限は要らない。タスクマネージャーのスタートアップで無効にした分(Windows 側の別の登録)には触らない。
/// 保存したときと、アプリの起動時(フォルダーを動かしたときに場所を合わせるため)に呼ぶ。
/// </summary>
internal static class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StarResonanceDps";

    /// <summary>オンなら書き、オフなら消す。書けなかった・消せなかったら false で、ログに残す。</summary>
    public static bool Apply(bool isEnabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (!isEnabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            if (Environment.ProcessPath is not { Length: > 0 } executablePath)
            {
                Log.Error("Could not register the app to launch when Windows starts: the executable path is unknown");
                return false;
            }

            key.SetValue(ValueName, $"\"{executablePath}\"");
            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not update the Windows startup registration (enabled: {IsEnabled})", isEnabled);
            return false;
        }
    }
}
