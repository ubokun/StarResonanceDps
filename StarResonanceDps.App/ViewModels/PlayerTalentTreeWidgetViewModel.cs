using System.Collections.ObjectModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using static StarResonanceDps.Core.CombatRuntime.DataTypes.Enums.Professions;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// アビリティ情報。プレイヤーのタレントの木をゲームと同じ形で描く。出す木は1本で、特化が分かればその特化のクラスR2 の木、
/// クラスR1 ならその職業のクラスR1 の木。特化が分からない・変身中・根のバフの一覧をまだ受けていなければ何も出さない。
///
/// <para>
/// 取っているアビリティは、乗っている根のバフ(名簿の行の <see cref="PlayerRosterEntry.RootBuffData"/>)と、
/// 自分なら取った節の一覧(<see cref="PlayerRosterEntry.TalentNodeIds"/>)で決める。一覧がある行は全部の節が決まる。
/// 一覧が無い行(ほかのプレイヤーと、全体コンテナの前の自分)では、バフを付けないアビリティが分からない。
/// 技の置き換えの節は最後に使った技(<see cref="PlayerRosterEntry.LastReplaceableSkills"/>)で決め、まだ使っていなければ分からない。
/// 分からないアビリティは背景と絵を灰色にして出す。
/// </para>
///
/// <para>
/// 線と木の大きさは木が替わったときだけ差し替える。節は名簿の行が入れ替わるたびに作り直して前の中身と値で比べ、違う節だけ差し替える。
/// 根のバフは時間では変わらないのでタイマーは持たない。
/// 相手が一覧に居なくても分かっている値(<c>PlayerDirectory</c>。一覧から外れた人は最後の値)で出す(プレイヤー情報と同じ)。
/// </para>
/// </summary>
public sealed class PlayerTalentTreeWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly ObservableCollection<TalentTreeNodeEntry> _nodes = [];

    private PlayerRosterEntry? _player;
    private TalentTreeDefinition? _tree;
    private IReadOnlyList<TalentTreeLine> _lines = [];
    private double _treeWidth;
    private double _treeHeight;
    private bool _isDisposed;

    public PlayerTalentTreeWidgetViewModel(
        WidgetListItemViewModel widget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(widget, requestedCharacterId)
    {
        Nodes = new ReadOnlyObservableCollection<TalentTreeNodeEntry>(_nodes);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
    }

    /// <summary>節。木の表の順。</summary>
    public ReadOnlyObservableCollection<TalentTreeNodeEntry> Nodes { get; }

    /// <summary>節をつなぐ線。木が替わったときだけ差し替える。</summary>
    public IReadOnlyList<TalentTreeLine> Lines
    {
        get => _lines;
        private set => SetProperty(ref _lines, value);
    }

    /// <summary>節の位置の右端(木の表の単位)。木が無ければ 0。</summary>
    public double TreeWidth
    {
        get => _treeWidth;
        private set => SetProperty(ref _treeWidth, value);
    }

    /// <summary>節の位置の下端(木の表の単位)。木が無ければ 0。</summary>
    public double TreeHeight
    {
        get => _treeHeight;
        private set => SetProperty(ref _treeHeight, value);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>相手が一覧に居なくても分かっている値で出す(プレイヤー情報と同じ)。</summary>
    protected override bool ShowsKnownPlayerOffList => true;

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForRootBuffs(
            reception,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            player,
            countsAdvancedType: false);

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var tree = ResolveTree(_player);
        if (!ReferenceEquals(tree, _tree))
        {
            _tree = tree;
            Lines = tree?.Lines ?? [];
            TreeWidth = tree?.Width ?? 0;
            TreeHeight = tree?.Height ?? 0;
        }

        var entries = CreateEntries(tree, _player);

        // 位置ごとに当て直し、増えたぶんだけ足し、減ったぶんだけ外す(中身が同じ節は差し替えない)。
        for (var index = 0; index < entries.Count; index++)
        {
            if (index < _nodes.Count)
            {
                if (!Equals(_nodes[index], entries[index]))
                {
                    _nodes[index] = entries[index];
                }
            }
            else
            {
                _nodes.Add(entries[index]);
            }
        }

        while (_nodes.Count > entries.Count)
        {
            _nodes.RemoveAt(_nodes.Count - 1);
        }
    }

    /// <summary>
    /// 出す木。特化が分かればその特化のクラスR2 の木、クラスR1 ならその職業のクラスR1 の木。
    /// 木の表はサーバーで違うので、行のサーバー(履歴の行はその回のサーバー)、無ければ今のサーバーで引く。
    /// 根のバフの一覧が未着・特化が分からない・変身中なら null。
    /// </summary>
    internal static TalentTreeDefinition? ResolveTree(PlayerRosterEntry? player)
    {
        if (player?.RootBuffData is null)
        {
            return null;
        }

        var server = player.GameServer ?? GameServerStore.Current;
        var subProfessionId = PlayerClassSpecResolver.ToSubProfessionId(player.ClassSpec);
        if (subProfessionId > 0)
        {
            return CombatDataCatalog.GetTalentTree(server, player.ProfessionId, subProfessionId);
        }

        return player.ClassSpec == PlayerClassSpec.Rank1
            ? CombatDataCatalog.GetTalentTree(server, player.ProfessionId, 0)
            : null;
    }

    private static List<TalentTreeNodeEntry> CreateEntries(TalentTreeDefinition? tree, PlayerRosterEntry? player)
    {
        if (tree is null || player?.RootBuffData is not { } rootBuffData)
        {
            return [];
        }

        var entries = new List<TalentTreeNodeEntry>(tree.Nodes.Count);
        foreach (var node in tree.Nodes)
        {
            if (node.IsSpecRoot)
            {
                entries.Add(new TalentTreeNodeEntry(
                    node.X,
                    node.Y,
                    TalentTreeNodeKind.SpecRoot,
                    CombatIconResolver.ResolveRootBuffIcon(RootBuffKind.Talent, node.Icon),
                    ERoleType.None,
                    CombatDataCatalog.GetTalentName(node.TalentId)));
                continue;
            }

            var kind = node.IsAbility ? ResolveAbilityKind(node, rootBuffData, player) : TalentTreeNodeKind.Dot;
            if (kind == TalentTreeNodeKind.Dot)
            {
                entries.Add(new TalentTreeNodeEntry(node.X, node.Y, TalentTreeNodeKind.Dot, null, ERoleType.None, string.Empty));
                continue;
            }

            // 括弧は4言語とも半角で、前に空白を入れない(ヘイトの TIPS と同じ形)。
            var talentName = CombatDataCatalog.GetTalentName(node.TalentId);
            entries.Add(new TalentTreeNodeEntry(
                node.X,
                node.Y,
                kind,
                CombatIconResolver.ResolveRootBuffIcon(RootBuffKind.Talent, node.Icon),
                node.Role,
                kind == TalentTreeNodeKind.Unknown
                    ? $"{talentName}({LocalizationManager.Instance.GetString("PlayerTalent_ActivationUnknown")})"
                    : talentName));
        }

        return entries;
    }

    /// <summary>
    /// アビリティの節の出し方。乗っている根のバフがあれば取っている。取った節の一覧がある行は一覧で決まり、分からない節は無い。
    /// 一覧が無い行では、バフを付ける節はバフが無ければ取っていない(根のバフは全部を受けている)。
    /// 技の置き換えの節は、どれかの置き換えで最後に使ったのが置き換え後の技なら取っている、全部の元の技を最後に使っていれば取っていない、
    /// まだ使っていない置き換えがあれば分からない。ほかの節(能力値の変換など)は分からない。
    /// </summary>
    internal static TalentTreeNodeKind ResolveAbilityKind(TalentTreeNode node, PlayerRootBuffData rootBuffData, PlayerRosterEntry player)
    {
        if (node.Buffs.Any(rootBuffData.BuffIds.Contains))
        {
            return TalentTreeNodeKind.Taken;
        }

        if (player.TalentNodeIds is { } nodeIds)
        {
            return nodeIds.Contains(node.Id) ? TalentTreeNodeKind.Taken : TalentTreeNodeKind.Dot;
        }

        if (node.Buffs.Count > 0)
        {
            return TalentTreeNodeKind.Dot;
        }

        if (node.SkillReplacements.Count == 0)
        {
            return TalentTreeNodeKind.Unknown;
        }

        var isKnown = true;
        foreach (var replacement in node.SkillReplacements)
        {
            if (player.LastReplaceableSkills is not { } lastSkills
                || !lastSkills.TryGetValue(replacement.OriginalSkillId, out var lastSkillId))
            {
                isKnown = false;
                continue;
            }

            if (lastSkillId == replacement.ReplacedSkillId)
            {
                return TalentTreeNodeKind.Taken;
            }
        }

        return isKnown ? TalentTreeNodeKind.Dot : TalentTreeNodeKind.Unknown;
    }
}
