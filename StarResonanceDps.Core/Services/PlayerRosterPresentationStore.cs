// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class PlayerRosterPresentationStore
{
    /// <summary>名前を伏せるときの表示。<b>UIDもまとめてこれ1つに置き換える。</b></summary>
    public const string HiddenPlayerName = "*****";

    private static readonly Lazy<PlayerRosterPresentationStore> LazyInstance = new(() => new PlayerRosterPresentationStore());

    private readonly object _sync = new();
    private readonly PlayerRosterStore _sourceStore = PlayerRosterStore.Instance;

    private IReadOnlyList<PlayerRosterEntry> _snapshot = Array.AsReadOnly(Array.Empty<PlayerRosterEntry>());
    private string _mapName = string.Empty;
    private uint _mapChannel;
    private long _mapGeneration;
    private int _seasonId;
    private PlayerNameDisplayMode _nameDisplayMode = PlayerNameDisplayMode.Show;

    private PlayerRosterPresentationStore()
    {
        _sourceStore.RosterChanged += SourceStore_RosterChanged;

        var current = _sourceStore.Current;
        _snapshot = CreatePresentationSnapshot(current.Entries, _nameDisplayMode);
        _mapName = current.MapName;
        _mapChannel = current.MapChannel;
        _mapGeneration = current.MapGeneration;
        _seasonId = current.SeasonId;
    }

    public static PlayerRosterPresentationStore Instance => LazyInstance.Value;

    public event EventHandler<PlayerRosterChangedEventArgs>? RosterChanged;

    public PlayerRosterSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return new PlayerRosterSnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration, _seasonId);
            }
        }
    }

    /// <summary>
    /// いまの名前の表示方法。ウィンドウのタイトルなど、
    /// ロスターの投影を通らない表示から使う。
    ///
    /// <para>
    /// 設定のプレビュー適用もここを通るので、<b>プレイヤー一覧と同じ瞬間に切り替わる</b>。
    /// </para>
    /// </summary>
    public PlayerNameDisplayMode NameDisplayMode
    {
        get
        {
            lock (_sync)
            {
                return _nameDisplayMode;
            }
        }
    }

    /// <summary>
    /// その人について分かっていること(<see cref="PlayerDirectory.Get"/>)を、一覧と同じく名前を伏せて返す。
    /// 一覧に載っていない人も引ける。何も分かっていなければ null。
    /// </summary>
    public PlayerRosterEntry? GetPlayer(long characterId)
    {
        var entry = PlayerDirectory.Get(characterId);
        if (entry is null)
        {
            return null;
        }

        var nameDisplayMode = NameDisplayMode;
        return ShouldHideName(entry, nameDisplayMode)
            ? entry with { Name = HiddenPlayerName }
            : entry;
    }

    public void SetNameDisplayMode(PlayerNameDisplayMode mode)
    {
        mode = NormalizeNameDisplayMode(mode);

        PlayerRosterSnapshot changedSnapshot;
        lock (_sync)
        {
            if (_nameDisplayMode == mode)
            {
                return;
            }

            _nameDisplayMode = mode;

            var current = _sourceStore.Current;
            _snapshot = CreatePresentationSnapshot(current.Entries, _nameDisplayMode);
            _mapName = current.MapName;
            _mapChannel = current.MapChannel;
            _mapGeneration = current.MapGeneration;
            _seasonId = current.SeasonId;
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration, _seasonId);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot));
    }

    private void SourceStore_RosterChanged(object? sender, PlayerRosterChangedEventArgs e)
    {
        PlayerRosterSnapshot changedSnapshot;
        lock (_sync)
        {
            _snapshot = CreatePresentationSnapshot(e.Snapshot, _nameDisplayMode);
            _mapName = e.MapName;
            _mapChannel = e.MapChannel;
            _mapGeneration = e.MapGeneration;
            _seasonId = e.SeasonId;
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration, _seasonId);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot));
    }

    private static IReadOnlyList<PlayerRosterEntry> CreatePresentationSnapshot(
        IReadOnlyList<PlayerRosterEntry> source,
        PlayerNameDisplayMode nameDisplayMode)
    {
        var entries = source
            .Select(entry => entry with
            {
                Name = ShouldHideName(entry, nameDisplayMode)
                    ? HiddenPlayerName
                    : entry.Name ?? string.Empty
            })
            .ToArray();

        return Array.AsReadOnly(entries);
    }

    private static bool ShouldHideName(PlayerRosterEntry entry, PlayerNameDisplayMode nameDisplayMode)
    {
        return nameDisplayMode switch
        {
            PlayerNameDisplayMode.Hide => true,
            PlayerNameDisplayMode.HideOthers => !entry.IsSelf,
            _ => false
        };
    }

    private static PlayerNameDisplayMode NormalizeNameDisplayMode(PlayerNameDisplayMode mode)
    {
        return mode switch
        {
            PlayerNameDisplayMode.Show => PlayerNameDisplayMode.Show,
            PlayerNameDisplayMode.Hide => PlayerNameDisplayMode.Hide,
            PlayerNameDisplayMode.HideOthers => PlayerNameDisplayMode.HideOthers,
            _ => PlayerNameDisplayMode.Show
        };
    }
}
