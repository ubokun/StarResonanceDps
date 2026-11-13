// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>通知を判定するバフの持ち主。プレイヤー(CharacterId)か、エンティティリストの実体(UUID)。</summary>
public readonly record struct BuffNotificationTarget(bool IsEntity, long Id)
{
    public static BuffNotificationTarget ForPlayer(long characterId) => new(false, characterId);

    public static BuffNotificationTarget ForEntity(long entityUuid) => new(true, entityUuid);
}

/// <summary>
/// バフの通知の判定(<see cref="BuffNotificationTracker"/>)を、対象とバフごとに1つにまとめる。
/// バフ・デバフカード・バフ・デバフグループ・通知の見張り(自分の料理・薬剤、<see cref="Services.NotificationMonitor"/>)が
/// 同じ対象の同じバフを見ているとき、別々に判定すると同じ通知が見ている数だけ出るため。
/// 見ているものが全部手放したら判定を捨てる(次に見始めたときは新しく作る)。画面のスレッドだけで使う。
///
/// <para>
/// バフ・デバフ効果切れは、窓(カード・グループ)が見ている間だけ、そのバフがその窓の種類の設定の「通知対象」に入っているときに出す。
/// 通知対象でない窓と見張りは、効果切れを出さない借り方をする(<c>notifiesExpiry</c>)。同じバフを見ている窓のどれかが出す借り方なら出す。
/// </para>
/// </summary>
public sealed class BuffNotificationRegistry
{
    private const string GroupKeyPrefix = "group:";

    private readonly Dictionary<(BuffNotificationTarget Target, string BuffKey), Entry> _entries = [];

    public static BuffNotificationRegistry Instance { get; } = new();

    /// <summary>
    /// 判定のバフの鍵。料理・薬剤のまとまりはまとまりの鍵、それ以外はカードの鍵(<see cref="PlayerBuffSnapshot.CardKey"/>)。
    /// カードとグループは料理・薬剤をどちらもまとまりで見るので、同じ鍵になる。
    /// </summary>
    public static string CreateBuffKey(BuffGroup group, string cardKey)
    {
        return group != BuffGroup.None ? GroupKeyPrefix + group : cardKey;
    }

    /// <summary>対象とバフの判定を借りる。使い終わったら返り値を Dispose する。</summary>
    /// <param name="notifiesExpiry">バフ・デバフ効果切れを出す借り方か。窓は通知対象なら true、通知の見張りは false。</param>
    public BuffNotificationWatch Watch(BuffNotificationTarget target, string buffKey, bool notifiesExpiry = true)
    {
        var key = (target, buffKey);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry();
            _entries.Add(key, entry);
        }

        entry.WatchCount++;
        if (notifiesExpiry)
        {
            entry.ExpiryWatchCount++;
        }

        return new BuffNotificationWatch(this, target, buffKey, entry, notifiesExpiry);
    }

    internal void Release(BuffNotificationTarget target, string buffKey, bool notifiesExpiry)
    {
        var key = (target, buffKey);
        if (!_entries.TryGetValue(key, out var entry))
        {
            return;
        }

        entry.WatchCount--;
        if (notifiesExpiry)
        {
            entry.ExpiryWatchCount--;
        }

        if (entry.WatchCount <= 0)
        {
            _entries.Remove(key);
        }
    }

    internal sealed class Entry
    {
        public BuffNotificationTracker Tracker { get; } = new();

        public int WatchCount { get; set; }

        /// <summary>効果切れを出す借り方(窓)の数。0 なら判定は効果切れを出さない。</summary>
        public int ExpiryWatchCount { get; set; }
    }
}

/// <summary>窓か通知の見張りが借りている、1つの対象の1つのバフの判定。</summary>
public sealed class BuffNotificationWatch : IDisposable
{
    private readonly BuffNotificationRegistry _registry;
    private readonly BuffNotificationRegistry.Entry _entry;
    private readonly bool _notifiesExpiry;
    private bool _isDisposed;

    internal BuffNotificationWatch(
        BuffNotificationRegistry registry,
        BuffNotificationTarget target,
        string buffKey,
        BuffNotificationRegistry.Entry entry,
        bool notifiesExpiry)
    {
        _registry = registry;
        Target = target;
        BuffKey = buffKey;
        _entry = entry;
        _notifiesExpiry = notifiesExpiry;
    }

    public BuffNotificationTarget Target { get; }

    public string BuffKey { get; }

    /// <summary>効果切れを出す借り方か。変えるときは借り直す。</summary>
    public bool NotifiesExpiry => _notifiesExpiry;

    /// <param name="snapshot">見つけたバフ。消えていれば null。</param>
    /// <param name="targetName">対象の名前(窓に出している名前と同じ規則)。</param>
    public void Observe(PlayerBuffSnapshot? snapshot, string targetName)
    {
        if (!_isDisposed)
        {
            _entry.Tracker.Observe(snapshot, targetName, _entry.ExpiryWatchCount > 0);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _registry.Release(Target, BuffKey, _notifiesExpiry);
    }
}

/// <summary>
/// 1つの対象の1つのバフだけを見る窓(バフ・デバフカード)の判定の借り方。
/// 対象かバフか効果切れを出すか(通知対象か)が替わったら前の判定を返して借り直し、対象が分からなくなったら返す。
/// </summary>
public sealed class BuffNotificationWatcher : IDisposable
{
    private BuffNotificationWatch? _watch;

    /// <param name="notifiesExpiry">バフ・デバフ効果切れを出すか(カードの設定の通知対象か)。</param>
    public void Observe(
        BuffNotificationTarget target,
        string buffKey,
        bool notifiesExpiry,
        PlayerBuffSnapshot? snapshot,
        string targetName)
    {
        if (_watch is null
            || _watch.Target != target
            || !string.Equals(_watch.BuffKey, buffKey, StringComparison.Ordinal)
            || _watch.NotifiesExpiry != notifiesExpiry)
        {
            // 先に借りてから返す(同じ対象とバフのまま借り方だけ変えるとき、判定の控えを捨てない)。
            var watch = BuffNotificationRegistry.Instance.Watch(target, buffKey, notifiesExpiry);
            _watch?.Dispose();
            _watch = watch;
        }

        _watch.Observe(snapshot, targetName);
    }

    /// <summary>対象かバフが分からなくなった・窓を閉じた。判定を返す(ほかの窓が見ていなければ捨てる)。</summary>
    public void Release()
    {
        _watch?.Dispose();
        _watch = null;
    }

    public void Dispose()
    {
        Release();
    }
}
