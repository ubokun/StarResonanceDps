// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Serilog;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 今キャプチャしているゲームのサーバー。ゲームの接続と分かるたびに、その接続を持つ実行ファイルの名前で置き換える
/// (<c>NetCap.GameConnectionIdentified</c>)。
///
/// <para>
/// ログアウト・キャプチャの停止・マップ移動では消さない。同じゲームのプロセスの間はサーバーが変わらず、次の接続で必ず書き直されるため。
/// </para>
/// </summary>
public static class GameServerStore
{
    private static readonly object StateLock = new();
    private static GameServer _current;

    /// <summary>今のサーバー。まだゲームの接続が無ければ分からない。</summary>
    public static GameServer Current
    {
        get
        {
            lock (StateLock)
            {
                return _current;
            }
        }
    }

    /// <summary>ゲームの接続を持つ実行ファイルの名前から、今のサーバーを置き換える。キャプチャのスレッドから呼ばれる。</summary>
    public static void SetFromExeName(string exeName)
    {
        var server = Utils.GameServerFromExeName(exeName);
        lock (StateLock)
        {
            if (_current == server)
            {
                return;
            }

            _current = server;
        }

        Log.Information("Game client {ExeName} identified as server {Server}", exeName, server);
    }
}
