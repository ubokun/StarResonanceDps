// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.HotkeyHost;

namespace StarResonanceDps.App.Config;

/// <summary>App のファイルの場所。基準は Core の <see cref="CombatRuntimePaths"/>(実行フォルダ)。</summary>
public static class AppDataPaths
{
    private const string AppSettingsFileName = "AppSettings.json";
    private const string WidgetSettingsFileName = "WidgetSettings.json";

    public static string AppSettingsPath => Path.Combine(CombatRuntimePaths.DataDirectory, AppSettingsFileName);

    public static string WidgetStatePath => Path.Combine(CombatRuntimePaths.DataDirectory, WidgetSettingsFileName);

    /// <summary>バフ・スキルのアイコンなど、同梱の画像の置き場。</summary>
    public static string ImagesDirectory => Path.Combine(CombatRuntimePaths.DataDirectory, "Images");

    // Windows の通知のアイコン。通知の処理が埋め込みから書き出す。Windows の通知の登録名はこのパスから作る
    // (Windows は登録名ごとに最初に読んだアイコンの場所を覚えて読み直さないため、場所を変えたら登録名も変わる形にしてある)。
    public static string WindowsNotificationIconPath => Path.Combine(ImagesDirectory, "Icon.png");

    // Plugin DLLs and every plugin-owned generated file share this one runtime directory.
    public static string PluginsDirectory => Path.Combine(CombatRuntimePaths.BaseDirectory, "Plugins");

    /// <summary>ホットキーを受ける補助の実行ファイル。ビルドで実行フォルダの直下へ写る。</summary>
    public static string HotkeyHostPath => Path.Combine(CombatRuntimePaths.BaseDirectory, HotkeyHostProtocol.ExecutableFileName);
}
