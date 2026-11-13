// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// ゲームの情報をどこまで受け取ったか。ウィジェットの状態の印(緑・黄・赤)の、自分と接続についての材料。
/// 印は「起きたこと」だけで赤・黄にする(既定は緑)ので、欠けはどれも、欠けたと分かる事実(途中から読んだ接続など)と組で表す。
/// </summary>
/// <param name="HasIdentifiedSelf">
/// キャプチャを始めてから(ログアウトから戻った後も)自分が分かった(自分宛ての差分・入場・全体コンテナのどれかを受けた)。
/// </param>
/// <param name="HasMidStreamConnection">
/// ゲームの接続を、始まり(SYN)を見ずに途中から読み始めた。始まりから読めば全体コンテナと入場は届くので、入場の情報が欠けたと言えるのはこのときだけ。
/// </param>
/// <param name="HasSelfContainer">自分の全体コンテナを受けた。</param>
/// <param name="HasSelfEnterScene">入場の全属性を受けた(<see cref="SelfAttributeChangeStore.HasFullSnapshot"/>)。</param>
public readonly record struct GameReceptionSnapshot(
    bool HasIdentifiedSelf,
    bool HasMidStreamConnection,
    bool HasSelfContainer,
    bool HasSelfEnterScene)
{
    /// <summary>途中起動: 接続を途中から読み始めて、全体コンテナがまだ届いていない。</summary>
    public bool IsMidLaunch => HasMidStreamConnection && !HasSelfContainer;

    /// <summary>接続を途中から読み始めて、入場の自分の情報(全体コンテナと入場の全属性)がそろっていない。</summary>
    public bool IsSelfEntryMissing => HasMidStreamConnection && (!HasSelfContainer || !HasSelfEnterScene);

    /// <summary>
    /// ゲームの情報が届き始めた(自分が分かったか、接続を途中から読み始めた)。
    /// 窓の相手が見つからないことは、これの後でだけ数える(起動の直後にまだ何も届いていないのは欠けではない)。
    /// </summary>
    public bool HasSessionStarted => HasIdentifiedSelf || HasMidStreamConnection;
}

/// <summary>
/// <see cref="GameReceptionSnapshot"/> の控え。印はパケットのスレッド(途中から読んだ接続はキャプチャのスレッド)で書き、
/// パケットを1つ処理し終えたとき(<see cref="Commit"/>)に組み直す。
///
/// <para>
/// 消すのはログアウトとキャプチャの停止(<see cref="Clear"/>)だけ。マップ移動では消さない。
/// </para>
/// </summary>
public static class GameReceptionStateStore
{
    private static readonly object Sync = new();

    private static bool _hasIdentifiedSelf;
    private static bool _hasMidStreamConnection;
    private static bool _hasSelfContainer;
    private static GameReceptionSnapshot _current;

    /// <summary>組み直した状態が変わったとき。パケットのスレッドか、キャプチャを止めたスレッドで上がる。</summary>
    public static event Action? Changed;

    public static GameReceptionSnapshot Current
    {
        get
        {
            lock (Sync)
            {
                return _current;
            }
        }
    }

    internal static void MarkSelfIdentified()
    {
        lock (Sync)
        {
            _hasIdentifiedSelf = true;
        }
    }

    /// <summary>ゲームの接続を途中から読み始めた。キャプチャのスレッドから呼ばれる。</summary>
    internal static void MarkMidStreamConnection()
    {
        lock (Sync)
        {
            _hasMidStreamConnection = true;
        }
    }

    internal static void MarkSelfContainerReceived()
    {
        lock (Sync)
        {
            _hasIdentifiedSelf = true;
            _hasSelfContainer = true;
        }
    }

    /// <summary>パケットを処理し終えた。状態を組み直し、変わっていれば知らせる。</summary>
    internal static void Commit()
    {
        var hasSelfEnterScene = SelfAttributeChangeStore.HasFullSnapshot;
        bool changed;
        lock (Sync)
        {
            var next = new GameReceptionSnapshot(_hasIdentifiedSelf, _hasMidStreamConnection, _hasSelfContainer, hasSelfEnterScene);
            changed = next != _current;
            _current = next;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// ログアウトとキャプチャの停止。印を全部下ろして組み直す(入場の全属性の印は先に消えている)。
    /// ログアウトの後も続く接続は開き直さないので、途中から読んだ印は付け直されない(次のログインは全体コンテナから始まる)。
    /// </summary>
    internal static void Clear()
    {
        lock (Sync)
        {
            _hasIdentifiedSelf = false;
            _hasMidStreamConnection = false;
            _hasSelfContainer = false;
        }

        Commit();
    }
}
