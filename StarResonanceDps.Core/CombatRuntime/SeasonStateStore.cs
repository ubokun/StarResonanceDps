// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 今のシーズンと、自分のシーズンごとのシーズンレベル。
///
/// <para>
/// 今のシーズンは通知 <c>SyncSeason</c> だけで決まる(ゲームも同じ値を今のシーズンにする)。
/// マップ移動のたびにフルコンテナの直後に届き、起動してから最初のマップ移動までは分からない。
/// </para>
///
/// <para>
/// 自分のシーズンレベルは、フルコンテナの「シーズン → レベル」の表を今のシーズンで引く。
/// <b>表の並びは届くたびに入れ替わるので、並びの位置(先頭・末尾)で選ばない。</b>
/// </para>
/// </summary>
public static class SeasonStateStore
{
    private static readonly object StateLock = new();
    private static int _currentSeasonId;
    private static Dictionary<int, int> _selfSeasonLevels = [];

    /// <summary>今のシーズン番号(<see cref="CurrentSeasonId"/>)が変わった。パケットのスレッドで上がる。</summary>
    public static event Action? Changed;

    /// <summary>今のシーズン番号。まだ届いていなければ 0。</summary>
    public static int CurrentSeasonId
    {
        get
        {
            lock (StateLock)
            {
                return _currentSeasonId;
            }
        }
    }

    public static void SetCurrentSeasonId(int seasonId)
    {
        bool changed;
        lock (StateLock)
        {
            changed = _currentSeasonId != seasonId;
            _currentSeasonId = seasonId;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>フルコンテナの自分のシーズンごとのレベルを控える。前の控えは捨てる。</summary>
    public static void ReplaceSelfSeasonLevels(IEnumerable<KeyValuePair<int, int>> levels)
    {
        var next = levels.ToDictionary(pair => pair.Key, pair => pair.Value);
        lock (StateLock)
        {
            _selfSeasonLevels = next;
        }
    }

    /// <summary>今のシーズンの自分のシーズンレベル。今のシーズンが分からないか、表に無ければ偽。</summary>
    public static bool TryGetSelfCurrentSeasonLevel(out int level)
    {
        lock (StateLock)
        {
            level = 0;
            return _currentSeasonId > 0
                && _selfSeasonLevels.TryGetValue(_currentSeasonId, out level);
        }
    }

    /// <summary>起動時の値に戻す(ログアウト)。キャラ交代があるので自分の控えも消す。</summary>
    public static void ResetToStartup()
    {
        bool changed;
        lock (StateLock)
        {
            changed = _currentSeasonId != 0;
            _currentSeasonId = 0;
            _selfSeasonLevels = [];
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }
}
