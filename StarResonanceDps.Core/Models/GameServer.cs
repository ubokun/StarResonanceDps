// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Models;

/// <summary>
/// ゲームのサーバー。キャプチャしたゲームの実行ファイル名で決める(<see cref="CombatRuntime.Utils.GameServerFromExeName"/>)。
/// 表の版はサーバーで違い、手元にあるのはアジアサーバー(StarASIA)と中国サーバー(Star)の表だけ。
/// </summary>
public enum GameServer
{
    /// <summary>分からない(まだゲームの接続が無い・カスタムの実行ファイル・サーバーを保存する前の履歴)。</summary>
    Unknown = 0,

    Asia,

    China,

    Global,

    SoutheastAsia
}
