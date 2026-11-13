// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>選択リストの項目が何を指すか。</summary>
public enum WidgetTargetOptionKind
{
    /// <summary>人(自分を含む)。</summary>
    Player,

    /// <summary>敵(エンティティリストの実体)。</summary>
    Entity,

    /// <summary>
    /// バフ・デバフカードのバフ・デバフ。相手は、料理・薬剤のまとまりなら自分、
    /// それ以外はバフリスト(デバフならデバフリスト)のカードで選んでいる項目。
    /// </summary>
    Buff,

    /// <summary>チャットのチャンネル。プライベートトークは相手1人(<see cref="WidgetTargetOption.CharacterId"/>)。</summary>
    Chat,

    /// <summary>プラグインのウィジェットの相手(<see cref="WidgetTargetOption.PluginTargetId"/>)。何を指すかは本体には分からない。</summary>
    Plugin
}

/// <summary>
/// メイン画面のカードの名前の選択リストの項目1つ。複数開けるウィジェット(<c>WidgetConfigDefaults.SupportsOpenTargets</c>)の、窓を開く相手。
/// リストを開いたときに作る。選んだ項目はカードが持ち、保存しない。
/// </summary>
public sealed class WidgetTargetOption
{
    public required WidgetTargetOptionKind Kind { get; init; }

    /// <summary>人: 自分(相手の指定なしで開く)。バフ: 相手がいつも自分のカード(ショートカット・「指定なし」とそれが決まった項目)。</summary>
    public bool IsSelf { get; init; }

    /// <summary>人: 相手の UID。自分なら分かっていれば自分のもの(0 はまだ分からない)。チャット: プライベートトークの相手の UID(ほかのチャンネルは 0)。</summary>
    public long CharacterId { get; init; }

    /// <summary>チャット: チャンネル。</summary>
    public ChitChatChannelType ChatChannel { get; init; }

    /// <summary>敵: 実体の値。実体を捕まえていない窓の項目と、敵を選んでいない項目は null。</summary>
    public EntityTargetInfo? Entity { get; init; }

    /// <summary>敵: 実体を捕まえていない窓(復元直後)の種別ID。0 は敵を選んでいない(<see cref="IsUnselectedEntity"/>)。</summary>
    public long UnacquiredEntityId { get; init; }

    /// <summary>敵: 実体を捕まえていない窓の、実体の種類。種別IDと組で比べる。</summary>
    public EEntityType? UnacquiredEntityType { get; init; }

    /// <summary>敵: 実体を捕まえていない窓の、保存しておいた名前。</summary>
    public string UnacquiredEntityName { get; init; } = string.Empty;

    /// <summary>バフ: カードの相手。開いている窓の項目だけが持つ(<see cref="WidgetTargetOptionKind.Buff"/> の相手の決め方より先に使う)。</summary>
    public WidgetTargetOption? BuffPerson { get; init; }

    /// <summary>プラグイン: 相手の ID。相手を選んでいない項目と空の窓は null(<see cref="IsUnselectedPlugin"/>)。</summary>
    public string? PluginTargetId { get; init; }

    /// <summary>プラグイン: 相手の名前(言語ごと)。</summary>
    public IReadOnlyDictionary<string, string>? PluginTargetNames { get; init; }

    /// <summary>プラグイン: プラグインが既定に指定した相手(手で選ぶまでカードがこれを選ぶ)。</summary>
    public bool IsDefaultPluginTarget { get; init; }

    /// <summary>バフ: バフかデバフか。</summary>
    public PlayerBuffListKind BuffListKind { get; init; }

    /// <summary>バフ: カードの鍵(<c>PlayerBuffSnapshot.CardKey</c>)。まとまりなら null。</summary>
    public string? BuffKey { get; init; }

    /// <summary>バフ: 料理・薬剤のまとまり。行から開くときは行のバフのまとまり(バフリストの行から開くのと同じ)。</summary>
    public BuffGroup BuffGroup { get; init; }

    /// <summary>バフ: 内部ID注記を付けない名前。カードの見出し(<c>BuffDebuffCardContent.CreateHeaderPrefix</c>)に使う。</summary>
    public string BuffName { get; init; } = string.Empty;

    /// <summary>リストの行の文字。その項目で開く窓のタイトルと同じ(<see cref="WidgetHeaderText"/>)。</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>リストの行の円の色。その項目の窓の状態の印で、窓が開いていなければ null(灰色)。リストを開いたときに決める。</summary>
    public WidgetDataStatusLevel? WindowStatusLevel { get; init; }

    /// <summary>同じ窓を指す項目か。リストを作り直しても選んでいる項目を保つのに使う。</summary>
    public bool IsSameTarget(WidgetTargetOption other)
    {
        if (Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            WidgetTargetOptionKind.Player => IsSelf == other.IsSelf && (IsSelf || CharacterId == other.CharacterId),
            WidgetTargetOptionKind.Entity => Entity is not null || other.Entity is not null
                ? Entity?.EntityUuid == other.Entity?.EntityUuid
                : UnacquiredEntityId == other.UnacquiredEntityId && UnacquiredEntityType == other.UnacquiredEntityType,
            WidgetTargetOptionKind.Chat => ChatChannel == other.ChatChannel && CharacterId == other.CharacterId,
            WidgetTargetOptionKind.Plugin => string.Equals(PluginTargetId, other.PluginTargetId, StringComparison.OrdinalIgnoreCase),
            _ => IsSelf == other.IsSelf
                && BuffListKind == other.BuffListKind
                && BuffGroup == other.BuffGroup
                && string.Equals(BuffKey, other.BuffKey, StringComparison.Ordinal)
                && (BuffPerson is null
                    ? other.BuffPerson is null
                    : other.BuffPerson is not null && BuffPerson.IsSameTarget(other.BuffPerson))
        };
    }

    /// <summary>同じ相手を指す、行の文字と円の色を付けた項目。</summary>
    public WidgetTargetOption WithPresentation(string label, WidgetDataStatusLevel? windowStatusLevel)
    {
        return new WidgetTargetOption
        {
            Kind = Kind,
            IsSelf = IsSelf,
            CharacterId = CharacterId,
            ChatChannel = ChatChannel,
            Entity = Entity,
            UnacquiredEntityId = UnacquiredEntityId,
            UnacquiredEntityType = UnacquiredEntityType,
            UnacquiredEntityName = UnacquiredEntityName,
            BuffPerson = BuffPerson,
            PluginTargetId = PluginTargetId,
            PluginTargetNames = PluginTargetNames,
            IsDefaultPluginTarget = IsDefaultPluginTarget,
            BuffListKind = BuffListKind,
            BuffKey = BuffKey,
            BuffGroup = BuffGroup,
            BuffName = BuffName,
            Label = label,
            WindowStatusLevel = windowStatusLevel
        };
    }

    /// <summary>自分(人の窓の既定)。</summary>
    public static WidgetTargetOption CreateSelf(long selfCharacterId)
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Player,
            IsSelf = true,
            CharacterId = selfCharacterId
        };
    }

    /// <summary>チャットのチャンネル。プライベートトークは相手の UID を渡す(ほかのチャンネルは 0)。</summary>
    public static WidgetTargetOption CreateChat(ChitChatChannelType channel, long partnerId)
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Chat,
            ChatChannel = channel,
            CharacterId = partnerId
        };
    }

    /// <summary>自分の料理・薬剤のまとまり。</summary>
    public static WidgetTargetOption CreateSelfBuffGroup(BuffGroup group)
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Buff,
            IsSelf = true,
            BuffListKind = PlayerBuffListKind.Buff,
            BuffGroup = group
        };
    }

    /// <summary>
    /// 自分のバフ・デバフのカード。開き方はリストの行から開くのと同じく行のバフのまとまりで、料理・薬剤ならまとまり(バフのキーを持たない)、
    /// それ以外はカードの鍵(<c>PlayerBuffSnapshot.CardKey</c>)。「指定なし」が決まったときに選択を置き換える項目。
    /// </summary>
    public static WidgetTargetOption CreateSelfBuffCard(PlayerBuffListKind kind, string cardKey, BuffGroup group, string buffName)
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Buff,
            IsSelf = true,
            BuffListKind = kind,
            BuffKey = group == BuffGroup.None ? cardKey : null,
            BuffGroup = group,
            BuffName = buffName
        };
    }

    /// <summary>
    /// バフ・デバフカードの「指定なし」(ショートカットが1つもオンでないときの既定)。起動ボタンは自分のバフリストの1行目
    /// (無ければデバフリストの1行目)のカードを開き、どちらも無ければ最初に付いたものを待つ窓を開く。
    /// バフもまとまりも指定しない形で、待っている窓(<c>BuffDebuffCardWidgetViewModel.IsWaiting</c>)もこれを指す。
    /// </summary>
    public static WidgetTargetOption CreateUnspecifiedBuffCard()
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Buff,
            IsSelf = true,
            BuffListKind = PlayerBuffListKind.Buff
        };
    }

    /// <summary>バフ・デバフカードの「指定なし」か(<see cref="CreateUnspecifiedBuffCard"/>)。</summary>
    public bool IsUnspecifiedBuffCard => Kind == WidgetTargetOptionKind.Buff && BuffKey is null && BuffGroup == BuffGroup.None;

    /// <summary>エンティティ情報で、敵を選んでいない項目(エンティティリストに敵がいないときの既定)。開く窓は敵を待つ空の窓。</summary>
    public static WidgetTargetOption CreateUnselectedEntity()
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Entity
        };
    }

    /// <summary>敵を選んでいない項目か(<see cref="CreateUnselectedEntity"/>)。</summary>
    public bool IsUnselectedEntity => Kind == WidgetTargetOptionKind.Entity && Entity is null && UnacquiredEntityId == 0;

    /// <summary>プラグインのウィジェットで、相手を選んでいない項目(相手の一覧を読む前と、一覧が空のときの既定)。開く窓は本体が出す空の窓。</summary>
    public static WidgetTargetOption CreateUnselectedPlugin()
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Plugin
        };
    }

    /// <summary>相手を選んでいない項目か(<see cref="CreateUnselectedPlugin"/>)。</summary>
    public bool IsUnselectedPlugin => Kind == WidgetTargetOptionKind.Plugin && PluginTargetId is null;
}
