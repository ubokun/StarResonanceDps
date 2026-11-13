// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// キャラIDごとの「分かっていること」を1か所で引く(<see cref="Get"/>)。プレイヤーの窓・チャットなど、どこから相手を開いても同じ値を見る。
///
/// <para>
/// 出所の優先の順は名簿の行と同じ。名簿の行(<see cref="PlayerRosterStore"/>。周りの実体・パーティ・メーターやタイムラインの記録から作る)を先に、
/// 空の項目だけを名刺照会の値(<see cref="SocialDataStore"/>)、チャットの発言者の値(<see cref="ChatMessageStore"/>)の順で埋める(<see cref="ApplyProfile"/>。名簿の行を作るときも同じ関数を通す)。
/// </para>
///
/// <para>
/// 名簿に載っていない人は、名簿から外れた人なら最後の行を周りにいない形にして(現在地は分からない)、一度も載っていない人なら名刺とチャットの値だけで作る。
/// プレイヤーリストに載せる人の決まりは名簿の側(<c>PlayerRosterProjection</c>)が持ち、ここは載せる・載せないを決めない。
/// </para>
///
/// <para>
/// 最後の行はログアウトで捨てる(<see cref="ResetToStartup"/>。キャラが替わり得る)。マップ移動とキャプチャの停止では捨てない。
/// </para>
/// </summary>
public static class PlayerDirectory
{
    private static readonly object StateLock = new();
    private static readonly Dictionary<long, PlayerRosterEntry> LastListedEntries = [];

    static PlayerDirectory()
    {
        PlayerRosterStore.Instance.RosterChanged += PlayerRosterStore_RosterChanged;
        SocialDataStore.Changed += characterId => Changed?.Invoke(characterId);
        ChatMessageStore.SenderIdentityChanged += characterId => Changed?.Invoke(characterId);
    }

    /// <summary>
    /// 名簿の外の値(名刺照会の値・チャットの発言者の値)が変わった相手。引数はキャラID。受信のスレッドから上がる。
    /// 名簿の行の変化は名簿の通知で届く。
    /// </summary>
    public static event Action<long>? Changed;

    /// <summary>その人について分かっていること。何も分かっていなければ null。名前は伏せる前の値。</summary>
    public static PlayerRosterEntry? Get(long characterId)
    {
        if (characterId <= 0)
        {
            return null;
        }

        var listed = PlayerRosterStore.Instance.Snapshot.FirstOrDefault(entry => entry.CharacterId == characterId);
        if (listed is not null)
        {
            return listed;
        }

        PlayerRosterEntry? lastListed;
        lock (StateLock)
        {
            LastListedEntries.TryGetValue(characterId, out lastListed);
        }

        if (lastListed is not null)
        {
            return ApplyProfile(lastListed with { IsNearby = false, PartySceneId = 0, PartyLineId = 0 });
        }

        if (SocialDataStore.GetProfile(characterId) is null
            && !ChatMessageStore.TryGetSenderIdentity(characterId, out _))
        {
            return null;
        }

        return ApplyProfile(new PlayerRosterEntry(
            characterId,
            string.Empty,
            0,
            IsLive: false,
            DataGaps: PlayerDataGaps.Identity | PlayerDataGaps.AppearAttributes | PlayerDataGaps.SkillLevels));
    }

    /// <summary>
    /// 行の空の項目を、名刺照会の値 → チャットの発言者の値の順で埋める。職業が埋まったらクラスを決め直し、
    /// 名刺照会で名前と職業が分かっていれば名前・クラスの欠けの印(<see cref="PlayerDataGaps.Identity"/>)を外す。
    /// 何も埋まらなければ同じ行を返す。
    /// </summary>
    internal static PlayerRosterEntry ApplyProfile(PlayerRosterEntry entry)
    {
        var profile = SocialDataStore.GetProfile(entry.CharacterId);
        ChatSenderIdentity? chatIdentity = ChatMessageStore.TryGetSenderIdentity(entry.CharacterId, out var identity)
            ? identity
            : null;
        if (profile is null && chatIdentity is null)
        {
            return entry;
        }

        var name = FirstNonEmpty(entry.Name, profile?.Name, chatIdentity?.Name);
        var professionId = FirstPositive(entry.ProfessionId, profile?.ProfessionId ?? 0);
        // 職業IDの無い変身(ミーン・ゴーレム)は、名刺の職業で元の職に戻さない。
        var classSpec = entry.ProfessionId == 0 && professionId > 0 && !PlayerClassSpecResolver.IsTransformation(entry.ClassSpec)
            ? PlayerClassSpecResolver.Resolve(
                professionId,
                entry.SubProfessionId,
                entry.ClassSpec == PlayerClassSpec.Rank1,
                buffTransformation: null)
            : entry.ClassSpec;
        var dataGaps = profile is { Name.Length: > 0, ProfessionId: > 0 }
            ? entry.DataGaps & ~PlayerDataGaps.Identity
            : entry.DataGaps;

        var filled = entry with
        {
            Name = name,
            ProfessionId = professionId,
            ClassSpec = classSpec,
            CombatPower = FirstPositive(entry.CombatPower, profile?.CombatPower ?? 0),
            SeasonStrength = FirstPositive(entry.SeasonStrength, profile?.SeasonStrength ?? 0),
            Level = FirstPositive(entry.Level, profile?.Level ?? 0, chatIdentity?.Level ?? 0),
            SeasonLevel = FirstPositive(entry.SeasonLevel, profile?.SeasonLevel ?? 0),
            EquipmentData = entry.EquipmentData ?? profile?.EquipmentData,
            DataGaps = dataGaps
        };
        return filled == entry ? entry : filled;
    }

    /// <summary>起動時の値に戻す(ログアウト)。名簿から外れた人の最後の行を捨てる。</summary>
    public static void ResetToStartup()
    {
        lock (StateLock)
        {
            LastListedEntries.Clear();
        }
    }

    private static void PlayerRosterStore_RosterChanged(object? sender, PlayerRosterChangedEventArgs e)
    {
        lock (StateLock)
        {
            foreach (var entry in e.Snapshot)
            {
                LastListedEntries[entry.CharacterId] = entry;
            }
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static int FirstPositive(params int[] values)
    {
        foreach (var value in values)
        {
            if (value > 0)
            {
                return value;
            }
        }

        return 0;
    }
}
