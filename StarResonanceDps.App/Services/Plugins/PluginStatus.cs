// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Services;

/// <summary>プラグインの一覧の行の状態。押せるのは <see cref="Openable"/> の行だけ。<see cref="Error"/> と <see cref="PendingDeletion"/> の行は灰色にする。</summary>
public enum PluginRowState
{
    /// <summary>窓を開くプラグイン(初期化の前も含む)。行を押すと窓を開く。</summary>
    Openable,

    /// <summary>窓の口を持たないプラグイン。</summary>
    NoWindow,

    /// <summary>読めない・初期化できない・止めた・削除できなかった。理由は <see cref="PluginErrorReason"/>。</summary>
    Error,

    /// <summary>アンインストールした。次の起動で、読み込む前にフォルダを消す。</summary>
    PendingDeletion,
}

/// <summary>一覧の行が <see cref="PluginRowState.Error"/> のときの理由。読み込めなかった理由は「プラグインエラー」の窓に出す(<see cref="PluginLoadFailure"/>)。</summary>
public enum PluginErrorReason
{
    /// <summary>要るファイルが足りない(フォルダに同じ名前の DLL が無いときも)。</summary>
    MissingFiles,

    /// <summary>このアプリのプラグインとして読めない(属性が足りない・版が違うなど)。</summary>
    Invalid,

    /// <summary>初期化か動いている途中の例外で止めた。次の起動でまた読む。</summary>
    Stopped,

    /// <summary>通知の項目の定義(<c>IPluginNotificationProvider</c>)が決まりに合わないので止めた。次の起動でまた読む。</summary>
    InvalidNotifications,

    /// <summary>ウィジェットの定義(<c>IPluginWidgetProvider</c>)が決まりに合わないので止めた。次の起動でまた読む。</summary>
    InvalidWidgets,

    /// <summary>通信の取り残しが上限を越えたので止めた。次の起動でまた読む。</summary>
    PacketBacklog,

    /// <summary>前の起動でアプリを終わらせた(フォルダの印)。入れ直すまで初期化しない。</summary>
    TerminatedApp,

    /// <summary>アンインストールの印があるのにフォルダを消せなかった。次の起動でまた消す。</summary>
    DeletionFailed,

    /// <summary>待っている更新を置き換えられなかった。次の起動でまた置き換える。</summary>
    UpdateFailed,
}

/// <summary>一覧の行1つの状態。</summary>
/// <param name="CanUninstall">ゴミ箱を押せるか。削除を待っている行と、削除できなかった行(次の起動でまた消す)は押せない。</param>
/// <param name="IsUpdatePending">新しいバージョンを選び、次の起動での置き換えを待っている。</param>
public sealed record PluginStatus(
    PluginRowState RowState,
    bool CanUninstall,
    bool IsUpdatePending);

/// <summary>「プラグインエラー」の窓に出す、読み込めなかったプラグイン1つ(起動時と、「＋」で足したとき)。</summary>
/// <param name="Name">一覧の行と同じ名前(今の言語)。</param>
/// <param name="MissingFiles"><see cref="PluginErrorReason.MissingFiles"/> のときの、足りないファイルの名前。</param>
public sealed record PluginLoadFailure(
    string Name,
    PluginErrorReason Reason,
    IReadOnlyList<string> MissingFiles);
